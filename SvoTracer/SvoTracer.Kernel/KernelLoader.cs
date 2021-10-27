using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SvoTracer.Kernel
{
    public static class KernelLoader
    {
        public static string Get(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
              .Single(str => str.EndsWith(fileName));

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
