using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FauxMessages;

namespace YAMLParser
{
    public class MsgFileLocation
    {
        // Unicode categories according to https://stackoverflow.com/a/950651/4036588
        private static readonly UnicodeCategory[] validFirstChars =
        { 
            UnicodeCategory.UppercaseLetter,    // Lu
            UnicodeCategory.LowercaseLetter,    // Ll
            UnicodeCategory.TitlecaseLetter,    // Lt
            UnicodeCategory.ModifierLetter,     // Lm
            UnicodeCategory.OtherLetter         // Lo
            // underscore is also permitted for the first char
        };
        private static readonly UnicodeCategory[] otherValidChars = 
        {
            UnicodeCategory.LetterNumber,           // Nl
            UnicodeCategory.NonSpacingMark,         // Mn
            UnicodeCategory.SpacingCombiningMark,   // Mc
            UnicodeCategory.DecimalDigitNumber,     // Nd
            UnicodeCategory.ConnectorPunctuation,   // Pc
            UnicodeCategory.Format                  // Cf
        };
        private static readonly UnicodeCategory[] validCSharpChars = validFirstChars.Union(otherValidChars).ToArray();

        private static string[] msg_gen_folder_names =
        {
            "msg",
            "srv",
            "msgs",
            "srvs"
        };

        public string path { get; private set; }
        public string basename { get; private set; }
        public string extension { get; private set; }
        public string package { get; private set; }
        public string packagedir { get; private set; }
        public string searchroot { get; private set; }

        public string Path
        {
            get { return path; }
        }

        public MsgFileLocation(string path,string root)
        {
            this.path = path;
            searchroot = root;
            packagedir = getPackagePath(root, path);
            package = getPackageName(path);
            extension = System.IO.Path.GetExtension(path).Trim('.');
            basename = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>
        /// Mangles a file's name to find the package name based on the name of the directory containing the file
        /// </summary>
        /// <param name="path">A file</param>
        /// <param name="targetmsgpath">The "package name"/"msg name" for the file at path</param>
        /// <returns>"package name"</returns>
        private static string getPackageName(string path)
        {
            DirectoryInfo innermostPath = Directory.GetParent(path);
            string foldername = innermostPath.Name;
            if (msg_gen_folder_names.Contains(foldername))
                foldername = Directory.GetParent(innermostPath.FullName).Name;

            if (!IsValidCSharpIdentifier(foldername))
                throw new ArgumentException(String.Format("'{0}' from '{1}' is not a compatible C# identifier name\n\tThe package name must conform to C# Language Specifications (refer to this StackOverflow answer: https://stackoverflow.com/a/950651/4036588)\n", foldername, path));

            return foldername;
        }

        private static string getPackagePath(string basedir, string msgpath)
        {
            string p = getPackageName(msgpath);
            return System.IO.Path.Combine(basedir, p);
        }

        public override bool Equals(object obj)
        {
            MsgFileLocation other = obj as MsgFileLocation;
            return (other != null && string.Equals(other.package, package) && string.Equals(other.basename, basename));
        }

        public override int GetHashCode()
        {
            return (package+"/"+basename).GetHashCode();
        }

        public override string  ToString()
        {
            return string.Format("{0}" + System.IO.Path.DirectorySeparatorChar + "{1}.{2}", package, basename, extension);
        }

        private static bool IsValidCSharpIdentifier(string toTest)
        {
            if (SingleType.IsCSharpKeyword(toTest)) // best to avoid any complications
                return false;
            
            char[] letters = toTest.ToCharArray();
            char first = letters[0];

            if (first != '_' && !validFirstChars.Contains(CharUnicodeInfo.GetUnicodeCategory(first)))
                return false;

            foreach (char c in letters)
                if (!validCSharpChars.Contains(CharUnicodeInfo.GetUnicodeCategory(c)))
                    return false;
            
            return true;
        }
    }

    internal static class MsgFileLocator
    {
        /// <summary>
        /// Finds all msgs and srvs below path and adds them to
        /// </summary>
        /// <param name="m"></param>
        /// <param name="s"></param>
        /// <param name="path"></param>
        private static void explode(List<MsgFileLocation> m, List<MsgFileLocation> s, string path)
        {
            string[] msgfiles = Directory.GetFiles(path, "*.msg", SearchOption.AllDirectories).ToArray();
            string[] srvfiles = Directory.GetFiles(path, "*.srv", SearchOption.AllDirectories).ToArray();
            Func<string, MsgFileLocation> conv = p => new MsgFileLocation(p,path);
            int mb4 = m.Count, sb4=s.Count;
            MsgFileLocation[] newmsgs = Array.ConvertAll(msgfiles, (p) => conv(p));
            MsgFileLocation[] newsrvs = Array.ConvertAll(srvfiles, (p) => conv(p));
            foreach(var nm in newmsgs)
                if (! m.Contains(nm))
                    m.Add(nm);
            foreach(var ns in newsrvs)
                if (!s.Contains(ns))
                    s.Add(ns);
            Console.WriteLine("Skipped " + (msgfiles.Length - (m.Count - mb4)) + " duplicate msgs and " + (srvfiles.Length - (s.Count - sb4)) + " duplicate srvs");
        }

        internal static int priority(string package)
        {
            switch (package)
            {
                case "std_msgs": return 1;
                case "geometry_msgs": return 2;
                case "actionlib_msgs": return 3;
                default: return 9;
            }
        }

        internal static List<MsgFileLocation> sortMessages(List<MsgFileLocation> msgs)
        {
            return msgs.OrderBy(m => "" + priority(m.package) + m.package + m.basename).ToList();
        }

        public static void findMessages(List<MsgFileLocation> msgs, List<MsgFileLocation> srvs, params string[] args)
        {
            //solution directory (where the reference to msg_gen is) is passed -- or assumed to be in a file in the same directory as the executable (which would be the case when msg_gen is directly run in the debugger
            if (args.Length == 0)
            {
                Console.WriteLine("MsgGen needs to receive a list of paths to recursively find messages in order to work.");
                Environment.Exit(1);
            }
            foreach (string arg in args)
            {
                explode(msgs, srvs, new DirectoryInfo(arg).FullName);
            }
        }
    }
}
