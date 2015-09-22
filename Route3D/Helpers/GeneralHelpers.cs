using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Media3D;

namespace Route3D.Helpers
{
    public static class GeneralHelpers
    {
        public static void Exchange<T>(ref T a, ref T b)
        {
            var t = a;
            a = b;
            b = t;
        }

        public static double RoundToEps(this double ths, double eps)
        {
            return Math.Round(ths/eps)*eps;
        }

        public static IEnumerable<IGrouping<int, T>> GroupByCount<T>(this IEnumerable<T> ths, int count)
        {
            int k = 0;
            var query = from s in ths
                        let num = k++
                        group s by num / count
                            into g
                            select g;

            return query;
        }

        public static T GetFirstDelegateFromName<T>(this object target, string methodName)
        {
            var inv = typeof(T).GetMethod("Invoke");

            if (inv == null)
                return default(T);

            var rt = inv.ReturnType;
            var args = inv.GetParameters().Select(x => x.ParameterType).ToArray();

            var method = target.GetType()
                .GetMethods(BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Instance
                            | BindingFlags.FlattenHierarchy)
                            .FirstOrDefault(x => x.Name.Equals(methodName, StringComparison.InvariantCultureIgnoreCase) && x.ReturnType == rt && args.SequenceEqual(x.GetParameters().Select(xy => xy.ParameterType).ToArray()));

            // Insert appropriate check for method == null here
            if (method == null)
                return default(T);

            return (T)(object)Delegate.CreateDelegate(typeof(T), target, method);
        }

        public static Type GetTypeFromName(string typeName)
        {
            Type type = null;

            // try to find manually
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = ass.GetType(typeName, false);

                if (type != null)
                    break;

            }
            return type;
        }

        /// <summary>
        /// Converts a .NET type into an XML compatible type - roughly
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string MapTypeToXmlType(Type type)
        {
            if (type == typeof(string) || type == typeof(char))
                return "string";
            if (type == typeof(int) || type == typeof(Int32))
                return "integer";
            if (type == typeof(long) || type == typeof(Int64))
                return "long";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(DateTime))
                return "datetime";

            if (type == typeof(float))
                return "float";
            if (type == typeof(decimal))
                return "decimal";
            if (type == typeof(double))
                return "double";
            if (type == typeof(Single))
                return "single";

            if (type == typeof(byte))
                return "byte";

            if (type == typeof(byte[]))
                return "base64Binary";

            return null;

            // *** hope for the best
            //return type.ToString().ToLower();
        }


        public static Type MapXmlTypeToType(string xmlType)
        {
            xmlType = xmlType.ToLower();

            if (xmlType == "string")
                return typeof(string);
            if (xmlType == "integer")
                return typeof(int);
            if (xmlType == "long")
                return typeof(long);
            if (xmlType == "boolean")
                return typeof(bool);
            if (xmlType == "datetime")
                return typeof(DateTime);
            if (xmlType == "float")
                return typeof(float);
            if (xmlType == "decimal")
                return typeof(decimal);
            if (xmlType == "double")
                return typeof(Double);
            if (xmlType == "single")
                return typeof(Single);

            if (xmlType == "byte")
                return typeof(byte);
            if (xmlType == "base64binary")
                return typeof(byte[]);


            // return null if no match is found
            // don't throw so the caller can decide more efficiently what to do 
            // with this error result
            return null;
        }

    }
}