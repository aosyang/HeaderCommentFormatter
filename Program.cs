using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;

namespace HeaderCommentFormatter
{
    class Program
    {
        static string[] IgnoredFolders = { ".git", ".vs", "lua5.3", "fbx", "tinyxml2", "shaders", "obj", "properties" };
        static string[] IgnoredFiles = { "resource.h", "assemblyinfo.cpp", "shareddefines.h", "ddstextureloader.h", "ddstextureloader.cpp" };
        static string[] SourceExts = { ".cpp", ".h", ".cs" };

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Execute(args[0]);
            }
            else
            {
                Console.WriteLine("Usage: {0} [path]", AppDomain.CurrentDomain.FriendlyName);
            }
        }

        /// <summary>
        /// Enumerate source files in the folder and run the formatting process
        /// </summary>
        /// <param name="RootPath"></param>
        private static void Execute(string RootPath)
        {
            foreach (string DirPath in Directory.EnumerateDirectories(RootPath))
            {
                string FolderName = Path.GetFileName(DirPath);
                if (IgnoredFolders.Contains(FolderName.ToLower()))
                {
                    continue;
                }

                foreach (string FilePath in Directory.EnumerateFiles(DirPath))
                {
                    string FileName = Path.GetFileName(FilePath);
                    if (!IgnoredFiles.Contains(FileName.ToLower()))
                    {
                        string FileExt = Path.GetExtension(FilePath);
                        if (SourceExts.Contains(FileExt.ToLower()) && !FilePath.Contains(".Designer."))
                        {
                            Console.WriteLine(FilePath);
                            ProcessSourceFile(FilePath);
                        }
                    }
                }

                Execute(DirPath);
            }
        }

        private static void ProcessSourceFile(string FilePath)
        {
            try
            {
                bool bFileProcessed = false;
                StreamReader sr = new StreamReader(FilePath);
                List<string> Lines = new List<string>();

                while (!sr.EndOfStream)
                {
                    Lines.Add(sr.ReadLine());
                }

                sr.Close();

                bFileProcessed |= ProcessHeaderComments(FilePath, ref Lines);
                if (Path.GetExtension(FilePath) == ".h")
                {
                    bFileProcessed |= ProcessHeaderGuards(FilePath, ref Lines);
                }

                if (bFileProcessed)
                {
                    StreamWriter sw = new StreamWriter(FilePath);

                    foreach (string Line in Lines)
                    {
                        sw.WriteLine(Line);
                    }

                    sw.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private static bool ProcessHeaderComments(string FilePath, ref List<string> Lines)
        {
            int StartLine = -1;
            int EndLine = -1;
            bool bHasCopyright = false;
            bool bHasHeaderComments = false;

            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Contains("//=="))
                {
                    if (StartLine == -1)
                    {
                        StartLine = i;
                    }
                    else if (EndLine == -1)
                    {
                        EndLine = i;
                    }
                }

                if (Lines[i].Contains("//") && Lines[i].Contains("All Rights Reserved"))
                {
                    bHasCopyright = true;
                }

                if (StartLine != -1 && EndLine != -1 && bHasCopyright)
                {
                    bHasHeaderComments = true;
                    break;
                }
            }

            if (!bHasHeaderComments)
            {
                string FileName = Path.GetFileName(FilePath);
                string UserName = UserPrincipal.Current.DisplayName;

                Lines.Insert(0, "//=============================================================================");
                Lines.Insert(1, string.Format("// {0} by {1}, {2} All Rights Reserved.", FileName, UserName, DateTime.Now.Year));
                Lines.Insert(2, "//");
                Lines.Insert(3, "//");
                Lines.Insert(4, "//=============================================================================");

                return true;
            }

            return false;
        }

        /// <summary>
        /// Replace header guard 'ifndef' to 'pragma once'
        /// </summary>
        /// <param name="FilePath"></param>
        /// <param name="Lines"></param>
        /// <returns></returns>
        private static bool ProcessHeaderGuards(string FilePath, ref List<string> Lines)
        {
            int StartCommentsLine = -1;
            int EndCommentsLine = -1;

            int LineIfndef = -1;
            int LineDefine = -1;
            int LineEndif = -1;
            string HeaderGuardString = "[Uninitialized]";

            for (int i = 0; i < Lines.Count; i++)
            {
                string LineContent = Lines[i];
                if (LineContent.Contains("//=="))
                {
                    if (StartCommentsLine == -1)
                    {
                        StartCommentsLine = i;
                    }
                    else if (EndCommentsLine == -1)
                    {
                        EndCommentsLine = i;
                    }
                }

                if (LineContent.Contains("#pragma once"))
                {
                    return false;
                }

                if (LineContent.Contains("#ifndef") && LineIfndef == -1 && LineContent.Contains("_"))
                {
                    char[] Spliter = { ' ', '\t' };
                    string[] Tokens = LineContent.Split(Spliter);
                    if (Tokens.Length == 2)
                    {
                        HeaderGuardString = Tokens[1];
                        LineIfndef = i;
                    }
                }

                if (LineIfndef != -1 && LineDefine == -1)
                {
                    if (LineContent.Contains("#define") && LineContent.Contains(HeaderGuardString))
                    {
                        LineDefine = i;
                    }
                }

                if (LineIfndef != -1 && LineDefine != -1)
                {
                    if (LineContent.Contains("#endif"))
                    {
                        LineEndif = i;
                    }
                }
            }

            if (LineIfndef != -1 && LineDefine != -1 && LineEndif != -1)
            {
                // Remove header guard lines from back to front
                Lines.RemoveAt(LineEndif);
                Lines.RemoveAt(LineDefine);
                Lines[LineIfndef] = "#pragma once";

                return true;
            }

            return false;
        }
    }
}
