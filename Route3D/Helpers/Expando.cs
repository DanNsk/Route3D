using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Route3D.Helpers;
using Route3D.Helpers.WPF;

namespace Route3D.Helpers
{

    [Serializable]
    public class Expando : DynamicObject, IDynamicMetaObjectProvider, IDisposable
    {
        private object instance;
        private Type instanceType;
        private PropertyInfo[] instancePropertyInfo;
        public PropertyBag Properties = new PropertyBag();

        protected virtual PropertyInfo[] InstancePropertyInfo
        {
            get
            {
                if (instancePropertyInfo == null && instance != null)
                    instancePropertyInfo = instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                return instancePropertyInfo;
            }
        }

        public object Instance
        {
            get { return instance; }
        }


        public Expando()
        {
            Initialize(this);
        }


        public Expando(object instance)
        {
            Initialize(instance);
        }



        protected virtual void Initialize(object inst)
        {
            this.instance = inst;

            if (inst != null)
                instanceType = inst.GetType();
        }



        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return GetProperties(true).Select(prop => prop.Key);
        }



        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = null;

            // first check the Properties collection for member
            if (Properties.Keys.Contains(binder.Name))
            {
                result = Properties[binder.Name];
                return true;
            }


            // Next check for Public properties via Reflection
            if (instance != null)
            {
                try
                {
                    return GetProperty(instance, binder.Name, out result);
                }
                catch
                {
                    //
                }
            }

            // failed to retrieve a property
            result = null;
            return false;
        }



        public override bool TrySetMember(SetMemberBinder binder, object value)
        {

            // first check to see if there's a native property to set
            if (instance != null)
            {
                try
                {
                    bool result = SetProperty(instance, binder.Name, value);
                    if (result)
                        return true;
                }
                catch
                {
                    //
                }
            }

            // no match - set or add to dictionary
            Properties[binder.Name] = value;
            return true;
        }


        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (instance != null)
            {
                try
                {
                    // check instance passed in for methods to invoke
                    if (InvokeMethod(instance, binder.Name, args, out result))
                        return true;
                }
                catch
                {
                    //
                }
            }

            result = null;
            return false;
        }



        protected bool GetProperty(object inst, string name, out object result)
        {
            if (inst == null)
                inst = this;

            var miArray = instanceType.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.Instance);
            if (miArray.Length > 0)
            {
                var mi = miArray[0];
                switch (mi.MemberType)
                {
                    case MemberTypes.Property:
                        result = ((PropertyInfo) mi).GetValue(inst, null);
                        return true;
                    case MemberTypes.Field:
                        result = ((FieldInfo)mi).GetValue(inst);
                        return true;
                }
            }

            result = null;
            return false;
        }


        protected virtual bool SetProperty(object inst, string name, object value)
        {
            if (inst == null)
                inst = this;

            var miArray = instanceType.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.SetField | BindingFlags.Instance);
            if (miArray.Length > 0)
            {
                var mi = miArray[0];
                if (mi.MemberType == MemberTypes.Property)
                {
                    ((PropertyInfo) mi).SetValue(inst, value, null);
                    return true;
                }
                else if (mi.MemberType == MemberTypes.Field)
                {
                    ((FieldInfo)mi).SetValue(inst, value);
                    return true;
                }
            }
            return false;
        }


        protected virtual bool InvokeMethod(object inst, string name, object[] args, out object result)
        {
            if (inst == null)
                inst = this;

            // Look at the instanceType
            var miArray = instanceType.GetMember(name,
                BindingFlags.InvokeMethod |
                BindingFlags.Public | BindingFlags.NonPublic| BindingFlags.Instance);

            if (miArray.Length > 0)
            {
                var mi = miArray[0] as MethodInfo;
                result = mi.Invoke(inst, args);
                return true;
            }

            result = null;
            return false;
        }





        public virtual object this[string key]
        {
            get
            {
                try
                {
                    // try to get from properties collection first
                    return Properties[key];
                }
                catch (KeyNotFoundException)
                {
                    // try reflection on instanceType
                    object result = null;
                    if (GetProperty(instance, key, out result))
                        return result;

                    // nope doesn't exist
                    throw;
                }
            }
            set
            {
                if (Properties.ContainsKey(key))
                {
                    Properties[key] = value;
                    return;
                }

                if (!SetProperty(instance, key, value))
                    Properties[key] = value;
            }
        }



        public virtual IEnumerable<KeyValuePair<string, object>> GetProperties(bool includeInstanceProperties = false)
        {
            if (includeInstanceProperties && instance != null)
            {
                foreach (var prop in this.InstancePropertyInfo)
                    yield return new KeyValuePair<string, object>(prop.Name, prop.GetValue(instance, null));
            }

            foreach (var key in this.Properties.Keys)
                yield return new KeyValuePair<string, object>(key, this.Properties[key]);

        }



        public virtual bool Contains(KeyValuePair<string, object> item, bool includeInstanceProperties = false)
        {
            bool res = Properties.ContainsKey(item.Key);
            if (res)
                return true;

            if (includeInstanceProperties && instance != null)
                return this.InstancePropertyInfo.Any(prop => prop.Name == item.Key);


            return false;
        }

        public void Dispose()
        {
            var disp = instance as IDisposable;

            if (disp != null && !Equals(disp, this))
            {
                disp.Dispose();
            }
        }
    }
}