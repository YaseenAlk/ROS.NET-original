// File: Program.cs
// Project: YAMLParser
// 
// ROS.NET
// Eric McCann <emccann@cs.uml.edu>
// UMass Lowell Robotics Laboratory
// 
// Reimplementation of the ROS (ros.org) ros_cpp client in C#.
// 
// Created: 04/28/2015
// Updated: 10/07/2015

#region USINGZ

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using FauxMessages;
using static FauxMessages.MsgsFile;

#endregion

namespace YAMLParser
{
    internal class Program
    {
        public static List<MsgsFile> msgsFiles = new List<MsgsFile>();
        public static List<SrvsFile> srvFiles = new List<SrvsFile>();
        public static string backhalf;
        public static string fronthalf;
        public static string name = "Messages";
        public static string outputdir = "Messages";
        public static string outputdir_secondpass = "TempSecondPass";
        private static string configuration = "Debug"; //Debug, Release, etc.
        private static void Main(string[] args)
        {
#if NET35
            string solutiondir;
            bool interactive = false; //wait for ENTER press when complete
            int firstarg = 0;
            if (args.Length >= 1)
            {
                if (args[firstarg].Trim().Equals("-i"))
                {
                    interactive = true;
                    firstarg++;
                }
                configuration = args[firstarg++];
                if (args[firstarg].Trim().Equals("-i"))
                {
                    interactive = true;
                    firstarg++;
                }
            }
            string yamlparser_parent = "";
            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
            while (di.Name != "YAMLParser")
            {
                di = Directory.GetParent(di.FullName);
            }
            di = Directory.GetParent(di.FullName);
            yamlparser_parent = di.FullName;
            if (args.Length - firstarg >= 1)
            {
                solutiondir = new DirectoryInfo(args[firstarg]).FullName;
            }
            else
            {
                solutiondir = yamlparser_parent;
            }

            outputdir = Path.Combine(solutiondir, outputdir);
            outputdir_secondpass = Path.Combine(solutiondir, outputdir_secondpass);
            List<MsgFileLocation> paths = new List<MsgFileLocation>();
            List<MsgFileLocation> pathssrv = new List<MsgFileLocation>();
            Console.WriteLine("Generatinc C# classes for ROS Messages...\n");
            for (int i = firstarg; i < args.Length; i++)
            {
                string d = new DirectoryInfo(args[i]).FullName;
                Console.WriteLine("Spelunking in " + d);
                MsgFileLocator.findMessages(paths, pathssrv, d);
            }
            paths = MsgFileLocator.sortMessages(paths);
            foreach (MsgFileLocation path in paths)
            {
                msgsFiles.Add(new MsgsFile(path));
            }
            foreach (MsgFileLocation path in pathssrv)
            {
                srvFiles.Add(new SrvsFile(path));
            }
            if (!StdMsgsProcessed()) // may seem obvious, but needed so that all other messages can resolve...
            {
                Console.WriteLine("std_msgs was not found in any search directory. Exiting...");
                return;
            }
            if (paths.Count + pathssrv.Count > 0)
            {
                MakeTempDir();
                GenerateFiles(msgsFiles, srvFiles);
                GenerateProject(msgsFiles, srvFiles);
                BuildProjectMSBUILD();
            }
            else
            {
                Console.WriteLine("Usage:         YAMLParser.exe <SolutionFolder> [... other directories to search]\n      The Messages dll will be output to <SolutionFolder>/Messages/Messages.dll");
                if (interactive)
                    Console.ReadLine();
                Environment.Exit(1);
            }
            if (interactive)
            {
                Console.WriteLine("Finished. Press enter.");
                Console.ReadLine();
            }
#elif NETCOREAPP2_1
            if (args.Length < 1)
            {
                Console.WriteLine("Usage:\tdotnet YAMLParser_NetCore.dll <DLL output path> [... other directories to search]\n\tThe Messages dll will be output to <DLL output folder>/Messages/Messages.dll");
                return;
            }

            string specifiedOutput = args[0];

            if (!Directory.Exists(specifiedOutput))
            {
                Console.WriteLine("DLL Output Path (" + specifiedOutput + ") does not exist");
                return;
            }

            outputdir = Path.Combine(specifiedOutput, outputdir);
            List<MsgFileLocation> msgFileLocs = new List<MsgFileLocation>();
            List<MsgFileLocation> srvFileLocs = new List<MsgFileLocation>();

            string[] searchDirectories = args.Skip(1).ToArray();
            foreach (string dir in searchDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine("Skipping directory '" + dir + "' because it does not exist.");
                    continue;
                }
                string d = new DirectoryInfo(dir).FullName;
                Console.WriteLine("Spelunking in " + d);
                MsgFileLocator.findMessages(msgFileLocs, srvFileLocs, d);
            }

            if ((msgFileLocs.Count + srvFileLocs.Count) == 0)
            {
                Console.WriteLine("Not going to generate Messages.dll because there were no .msg or .srv files found in the specified arguments");
                return;
            }

            Console.WriteLine("Generating Messages C# project...");
            msgFileLocs = MsgFileLocator.sortMessages(msgFileLocs);
            foreach (MsgFileLocation path in msgFileLocs)
            {
                msgsFiles.Add(new MsgsFile(path));
            }
            foreach (MsgFileLocation path in srvFileLocs)
            {
                srvFiles.Add(new SrvsFile(path));
            }

            if (!StdMsgsProcessed()) // may seem obvious, but needed so that all other messages can resolve...
            {
                Console.WriteLine("std_msgs was not found in any search directory. Exiting...");
                return;
            }

            Console.WriteLine("making temp dir...");
            MakeTempDir();
            Console.WriteLine("Generating files...");
            GenerateFiles(msgsFiles, srvFiles);
            Console.WriteLine("Generating project...");
            GenerateProject(msgsFiles, srvFiles);
            Console.WriteLine("Building project...");
            Console.WriteLine("------------------------------------");
            BuildProjectNETCORE();
            Console.WriteLine("------------------------------------");
            Console.WriteLine("Done!");
#else
            Console.WriteLine("Unsupported TargetFramework. Must be .NET Framework v3.5 or .NET Core netcoreapp2.1");
            return;
#endif
        }

        public static bool StdMsgsProcessed()
        {
            return MsgsFile.resolver.ContainsKey("std_msgs");
        }

        public static void MakeTempDir()
        {
            if (!Directory.Exists(outputdir))
                Directory.CreateDirectory(outputdir);
            else
            {
                foreach (string s in Directory.GetFiles(outputdir, "*.cs"))
                    try
                    {
                        File.Delete(s);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                foreach (string s in Directory.GetDirectories(outputdir))
                    if (s != "Properties")
                        try
                        {
                            Directory.Delete(s, true);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
            }
            if (!Directory.Exists(outputdir)) Directory.CreateDirectory(outputdir);
            else
            {
                foreach (string s in Directory.GetFiles(outputdir, "*.cs"))
                    try
                    {
                        File.Delete(s);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                foreach (string s in Directory.GetDirectories(outputdir))
                    if (s != "Properties")
                        try
                        {
                            Directory.Delete(s, true);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
            }
        }

        public static void GenerateFiles(List<MsgsFile> files, List<SrvsFile> srvfiles)
        {
            List<MsgsFile> mresolved = new List<MsgsFile>();
            List<SrvsFile> sresolved = new List<SrvsFile>();
            while (files.Except(mresolved).Any())
            {
                Debug.WriteLine("MSG: Running for " + files.Count + "/" + mresolved.Count + "\n" + files.Except(mresolved).Aggregate("\t", (o, n) => "" + o + "\n\t" + n.Name));
                foreach (MsgsFile m in files.Except(mresolved))
                {
                    string md5 = null;
                    string typename = null;
                    md5 = MD5.Sum(m);
                    typename = m.Name;
                    if (md5 != null && !md5.StartsWith("$") && !md5.EndsWith("MYMD5SUM"))
                    {
                        mresolved.Add(m);
                    }
                    else
                    {
                        Debug.WriteLine("Waiting for children of " + typename + " to have sums");
                    }
                }
                if (files.Except(mresolved).Any())
                {
                    Debug.WriteLine("MSG: Rerunning sums for remaining " + files.Except(mresolved).Count() + " definitions");
                }
            }
            while (srvfiles.Except(sresolved).Any())
            {
                Debug.WriteLine("SRV: Running for " + srvfiles.Count + "/" + sresolved.Count + "\n" + srvfiles.Except(sresolved).Aggregate("\t", (o, n) => "" + o + "\n\t" + n.Name));
                foreach (SrvsFile s in srvfiles.Except(sresolved))
                {
                    string md5 = null;
                    string typename = null;
                    s.Request.Stuff.ForEach(a => s.Request.resolve(s.Request, a));
                    s.Response.Stuff.ForEach(a => s.Request.resolve(s.Response, a));
                    md5 = MD5.Sum(s);
                    typename = s.Name;
                    if (md5 != null && !md5.StartsWith("$") && !md5.EndsWith("MYMD5SUM"))
                    {
                        sresolved.Add(s);
                    }
                    else
                    {
                        Debug.WriteLine("Waiting for children of " + typename + " to have sums");
                    }
                }
                if (srvfiles.Except(sresolved).Any())
                {
                    Debug.WriteLine("SRV: Rerunning sums for remaining " + srvfiles.Except(sresolved).Count() + " definitions");
                }
            }
            foreach (MsgsFile file in files)
            {
                file.Write(outputdir);
            }
            foreach (SrvsFile file in srvfiles)
            {
                file.Write(outputdir);
            }
            File.WriteAllText(Path.Combine(outputdir, "MessageTypes.cs"), ToString().Replace("FauxMessages", "Messages"));
        }

        public static void GenerateProject(List<MsgsFile> files, List<SrvsFile> srvfiles)
        {
            if (!Directory.Exists(Path.Combine(outputdir, "Properties")))
                Directory.CreateDirectory(Path.Combine(outputdir, "Properties"));

            string sHelperFile, interfacesFile, assemblyInfoFile;
            string lineFile;
#if NET35
            sHelperFile = Templates.SerializationHelper;
            interfacesFile = Templates.Interfaces;
            assemblyInfoFile = Templates.AssemblyInfo;
            lineFile = Templates.MessagesProj;
#elif NETCOREAPP2_1
            sHelperFile = "";   // blank in the original version?
            interfacesFile = Templates_NetCore.Interfaces;
            assemblyInfoFile = Templates_NetCore.AssemblyInfo;
            lineFile = Templates_NetCore.MessagesProj;
#else
            throw new PlatformNotSupportedException("Unsupported TargetFramework. Must be .NET Framework v3.5 or .NET Core netcoreapp2.1");
#endif

            File.WriteAllText(Path.Combine(outputdir, "SerializationHelper.cs"), sHelperFile);
            File.WriteAllText(Path.Combine(outputdir, "Interfaces.cs"), interfacesFile);
            File.WriteAllText(Path.Combine(outputdir, "Properties", "AssemblyInfo.cs"), assemblyInfoFile);
            string[] lines = lineFile.Split('\n');
            string output = "";
            for (int i = 0; i < lines.Length; i++)
            {
#if FOR_UNITY
                if (lines[i].Contains("TargetFrameworkProfile"))
                    output += "<TargetFrameworkProfile>Unity Full v3.5</TargetFrameworkProfile>\n";
                else
#endif
                {
                    output += "" + lines[i] + "\n";
                }

                if (lines[i].Contains("<Compile Include="))
                {
                    foreach (MsgsFile m in files)
                    {
                        output += "\t<Compile Include=\"" + m.Name.Replace('.', '\\') + ".cs\" />\n";
                    }
                    foreach (SrvsFile m in srvfiles)
                    {
                        output += "\t<Compile Include=\"" + m.Name.Replace('.', '\\') + ".cs\" />\n";
                    }
                    output += "\t<Compile Include=\"SerializationHelper.cs\" />\n";
                    output += "\t<Compile Include=\"Interfaces.cs\" />\n";
                    output += "\t<Compile Include=\"MessageTypes.cs\" />\n";
                }
            }
            File.WriteAllText(Path.Combine(outputdir, name + ".csproj"), output);
            File.WriteAllText(Path.Combine(outputdir, ".gitignore"), "*");
        }

        private static string __where_be_at_my_vc____is;

        public static string VCDir
        {
            get
            {
                if (__where_be_at_my_vc____is != null) return __where_be_at_my_vc____is;
                foreach (string possibledir in new[] { Path.DirectorySeparatorChar + Path.Combine("Microsoft.NET", "Framework64") + Path.DirectorySeparatorChar, Path.DirectorySeparatorChar + Path.Combine("Microsoft.NET", "Framework") + Path.DirectorySeparatorChar })
                {
                    foreach (string possibleversion in new[] {"v3.5", "v4.0"})
                    {
                        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.System) + Path.DirectorySeparatorChar + ".." + possibledir)) continue;
                        foreach (string dir in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.System) + Path.DirectorySeparatorChar + ".." + possibledir))
                        {
                            if (!Directory.Exists(dir)) continue;
                            string[] tmp = dir.Split(Path.DirectorySeparatorChar);
                            if (tmp[tmp.Length - 1].Contains(possibleversion))
                            {
                                __where_be_at_my_vc____is = dir;
                                return __where_be_at_my_vc____is;
                            }
                        }
                    }
                }
                return __where_be_at_my_vc____is;
            }
        }

        public static void BuildProjectMSBUILD()
        {
            BuildProjectMSBUILD("BUILDING GENERATED PROJECT WITH MSBUILD!");
        }

        public static void BuildProjectMSBUILD(string spam)
        {
            string F = VCDir + Path.DirectorySeparatorChar + "msbuild.exe";
            if (!File.Exists(F))
            {
                Exception up = new Exception("ALL OVER YOUR FACE\n" + F);
                throw up;
            }
            Console.WriteLine("\n\n" + spam);
            string args = "/nologo \"" + Path.Combine(outputdir, name + ".csproj") + Path.DirectorySeparatorChar + " /property:Configuration=" + configuration;
            Process proc = new Process();
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = F;
            proc.StartInfo.Arguments = args;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            if (File.Exists(Path.Combine(outputdir, "bin", configuration, name + ".dll")))
            {
                Console.WriteLine("\n\nGenerated DLL has been copied to:\n\t" + Path.Combine(outputdir, name + ".dll") + "\n\n");
                File.Copy(Path.Combine(outputdir, "bin", configuration, name + ".dll"), Path.Combine(outputdir, name + ".dll"), true);
                Thread.Sleep(100);
            }
            else
            {
                if (output.Length > 0)
                    Console.WriteLine(output);
                if (error.Length > 0)
                    Console.WriteLine(error);
                Console.WriteLine("AMG BUILD FAIL!");
            }
        }

        public static void BuildProjectNETCORE()
        {
            //var process = new Process
            //{
            //    StartInfo = new ProcessStartInfo
            //    {
            //        FileName = "dotnet",
            //        Arguments = "path\release\PublishOutput\proces.dll",
            //        UseShellExecute = true,
            //        RedirectStandardOutput = false,
            //        RedirectStandardError = false,
            //        CreateNoWindow = true
            //    }

            //};

            Process process = new Process();

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "publish -c Release -v q",
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = outputdir,
                RedirectStandardOutput = false, //setting this to true makes process.WaitForExit() wait forever...
                RedirectStandardError = false
            };

            process.StartInfo = info;
            process.Start();
            process.WaitForExit();
        }

        private static string uberpwnage;

        public new static string ToString()
        {
            if (uberpwnage == null)
            {
                if (fronthalf == null)
                {
                    fronthalf = "using Messages;\n\nnamespace Messages\n{\n";
                    backhalf = "\n}";
                }

                List<MsgsFile> everything = new List<MsgsFile>(msgsFiles);
                foreach (SrvsFile sf in srvFiles)
                {
                    everything.Add(sf.Request);
                    everything.Add(sf.Response);
                }
                fronthalf += "\n\tpublic enum MsgTypes\n\t{";
                fronthalf += "\n\t\tUnknown,";
                string srvs = "\n\t\tUnknown,";
                for (int i = 0; i < everything.Count; i++)
                {
                    fronthalf += "\n\t\t";
                    if (everything[i].classname == "Request" || everything[i].classname == "Response")
                    {
                        if (everything[i].classname == "Request")
                        {
                            srvs += "\n\t\t" + everything[i].Name.Replace(".", "__") + ",";
                        }
                        everything[i].Name += "." + everything[i].classname;
                    }
                    fronthalf += everything[i].Name.Replace(".", "__");
                    if (i < everything.Count - 1)
                        fronthalf += ",";
                }
                fronthalf += "\n\t}\n";
                srvs = srvs.TrimEnd(',');
                fronthalf += "\n\tpublic enum SrvTypes\n\t{";
                fronthalf += srvs + "\n\t}\n";
                uberpwnage = fronthalf + backhalf;
            }
            return uberpwnage;
        }

        public static void GenDict(string dictname, string keytype, string valuetype, ref string appendto, int start, int end,
            Func<int, string> genKey)
        {
            GenDict(dictname, keytype, valuetype, ref appendto, start, end, genKey, null, null);
        }

        public static void GenDict(string dictname, string keytype, string valuetype, ref string appendto, int start, int end,
            Func<int, string> genKey, Func<int, string> genVal)
        {
            GenDict(dictname, keytype, valuetype, ref appendto, start, end, genKey, genVal, null);
        }


        public static void GenDict
            (string dictname, string keytype, string valuetype, ref string appendto, int start, int end,
                Func<int, string> genKey, Func<int, string> genVal, string DEFAULT)
        {
            appendto +=
                string.Format("\n\t\tpublic static Dictionary<{1}, {2}> {0} = new Dictionary<{1}, {2}>()\n\t\t{{",
                    dictname, keytype, valuetype);
            if (DEFAULT != null)
                appendto += "\n\t\t\t{" + DEFAULT + ",\n";
            for (int i = start; i < end; i++)
            {
                if (genVal != null)
                    appendto += string.Format("\t\t\t{{{0}, {1}}}{2}", genKey(i), genVal(i), (i < end - 1 ? ",\n" : ""));
                else
                    appendto += string.Format("\t\t\t{{{0}}}{1}", genKey(i), (i < end - 1 ? ",\n" : ""));
            }
            appendto += "\n\t\t};";
        }
    }

#if NETCOREAPP2_1
    public static class Templates_NetCore
    {
        public static string SrvPlaceHolder = GetResource("YAMLParser_NetCore.TemplateProject.SrvPlaceHolder._cs");
        public static string MsgPlaceHolder = GetResource("YAMLParser_NetCore.TemplateProject.PlaceHolder._cs");
        public static string Interfaces = GetResource("YAMLParser_NetCore.TemplateProject.Interfaces.cs");
        public static string AssemblyInfo = GetResource("YAMLParser_NetCore.TemplateProject.AssemblyInfo._cs");
        public static string MessagesProj = GetResource("YAMLParser_NetCore.TemplateProject.MessagesNetCore._csproj");

        private static string GetResource(string location)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(location))
            {
                TextReader tr = new StreamReader(stream);
                string fileContents = tr.ReadToEnd();
                return fileContents;
            }
        }
    }
#endif
}