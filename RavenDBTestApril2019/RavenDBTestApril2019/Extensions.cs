using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;
using Console = Colorful.Console;

namespace RavenDBTestApril2019
{
    public static class Extensions
    {

        public static string ToJSON(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static string ToJSONPretty(this object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        public static string ToJSONPrettySansNulls(this object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }


        public static bool HasValue(this string s)
        {
            return (!string.IsNullOrWhiteSpace(s));

        }

        public static bool HasValue(this DateTime dt)
        {
            if (dt == DateTime.MinValue) return false;
            return true;

        }

        public static bool HasValue(this bool? bValue)
        {
            return bValue.IsNotNull();

        }

        public static bool IsNotNull<T>(this T value)
        {
            return value != null;
        }

        public static bool IsNull<T>(this T value)
        {
            return value == null;
        }


        public static bool HasValues(this IEnumerable items)
        {
            if (items == null)
            {
                return false;
            }

            if (items.Cast<object>().Any())
            {
                return true;
            }

            return false;

        }

        public static bool HasNoValues(this IEnumerable items)
        {
            return !items.HasValues();
        }


        public static void WriteLine(this string message, Color? color = null, bool showTimeStamp = false)
        {

            if (Environment.UserInteractive == false) return;
            if (color == null) color = Color.White;

            if (message.HasValue())
                if (showTimeStamp)
                {
                    Console.WriteLine(value: $"{DateTime.Now:T} {message}", color: color.Value);
                }

                else
                    Console.WriteLine($"{message}", color.Value);
            else
                Console.WriteLine("");
        }

        public static void Write(this string message, Color? color = null, bool showTimeStamp = false)
        {

            if (Environment.UserInteractive == false) return;
            if (color == null) color = Color.White;

            if (message.HasValue())
                if (showTimeStamp)
                    Console.Write($"{DateTime.Now:T} {message}", color.Value);
                else
                    Console.Write($"{message}", color.Value);
        }


    }
}