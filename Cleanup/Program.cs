using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Cleanup
{
    class Program
    {
        static int totalFiles = 0;
        static long totalSize = 0;
        static int totalDirectories;
        static int totalErrors = 0;
        static int maxAge = 0;
        static bool optDeleteEmpty = false;
        static int optDeleteEmptyLevel = 1;
        static bool optRecurse = false;
        static bool optSimulate = false;
        static bool optLog = false;
        static string logFilename = null;
        static StreamWriter logFile;
        static string rootDirectory;
        static int rootDirectoryDepth;
        static bool optFilter = false;
        static bool optExcludeFilter = false;
        static string filter;
        static string filterRegEx;
        static string excludeFilter;
        static string excludeFilterRegEx;
        static string ignoreFile = ".cleanupignore";

        static void Main(string[] args)
        {
            string parameters = "";

            // Get arguments from command-line
            try
            {
                rootDirectory = args[0];

                // Remove trailing slash from rootDirectory (needed for correct depth calculation)
                if (rootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())) rootDirectory = rootDirectory.TrimEnd(Path.DirectorySeparatorChar);

                maxAge = int.Parse(args[1]);
                foreach (string x in args)
                {
                    parameters += x + " ";
                    if (x.ToUpper().Equals("/D")) optDeleteEmpty = true;
                    else if (x.ToUpper().StartsWith("/D:"))
                    {
                        optDeleteEmpty = true;
                        optDeleteEmptyLevel = int.Parse(x.Substring(3));
                    }
                    else if (x.ToUpper().Equals("/R")) optRecurse = true;
                    else if (x.ToUpper().Equals("/SIM")) optSimulate = true;
                    else if (x.ToUpper().StartsWith("/LOG"))
                    {
                        optLog = true;
                        if (x.ToUpper().StartsWith("/LOG:")) logFilename = x.Substring(5);
                        else logFilename = "Cleanup.log";
                    }
                    else if (x.ToUpper().StartsWith("/F:"))
                    {
                        optFilter = true;
                        filter = x.Substring(3).ToLower();
                        filterRegEx = Regex.Escape(filter);
                        filterRegEx = "^" + filterRegEx.Replace("\\*", ".+").Replace("\\?", ".") + "$";
                    }
                    else if (x.ToUpper().StartsWith("/EF:"))
                    {
                        optExcludeFilter = true;
                        excludeFilter = x.Substring(4).ToLower();
                        excludeFilterRegEx = Regex.Escape(excludeFilter);
                        excludeFilterRegEx = "^" + excludeFilterRegEx.Replace("\\*", ".+").Replace("\\?", ".") + "$";
                    }
                    else if (x != args[0] && x != args[1])
                    {
                        Console.WriteLine("Incorrect parameter: {0}<-\n", parameters);
                        ShowUsage();
                        return;
                    }
                }
            }
            catch
            {
                // Show Usage when something wrong/missing in arguments
                ShowUsage();
                return;
            }

            // Check if direcotry exists
            if (!Directory.Exists(rootDirectory))
            {
                Console.WriteLine("Directory {0} does not exist.\n", rootDirectory);
                return;
            }

            // Open and check if logfile is writable
            if (optLog)
            {
                try
                {
                    logFile = File.AppendText(logFilename);
                    logFile.WriteLine("");
                }
                catch
                {
                    Console.WriteLine("Unable to write to logfile {0}.\n", logFilename);
                    return;
                }
            }

            // Get rootDirecotry depth
            rootDirectoryDepth = Path.GetFullPath(rootDirectory).Split(Path.DirectorySeparatorChar).Length;

            // Go do stuff
            Output("Cleanup started with parameters: {0}", parameters);
            CleanupDirectory(rootDirectory);

            // Show summary
            if (optSimulate) Output("\nWould have deleted {0} files with a total size of {1}.", totalFiles, FormatSize(totalSize));
            else Output("\nDeleted {0} files ({1}) and {2} directories, encountered {3} errors.", totalFiles, FormatSize(totalSize), totalDirectories, totalErrors);

            logFile.Close();
        }

        static void CleanupDirectory(string directory)
        {
            int depth = Path.GetFullPath(directory).Split(Path.DirectorySeparatorChar).Length - rootDirectoryDepth;

            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            // Ignore current directory if ignoreFile exists
            string ignoreFilePath = Path.Combine(dirInfo.FullName, ignoreFile);
            if (File.Exists(ignoreFilePath))
            {
                Output(" Skipping directory ({1} file exists): {0}", directory, ignoreFile);
                return;
            }

            // Process directories
            DirectoryInfo[] dirs = dirInfo.GetDirectories();

            foreach (DirectoryInfo dir in dirs)
            {
                // Recurse subdirectories
                if (optRecurse) CleanupDirectory(dir.FullName);
            }


            // Process files in directory
            FileInfo[] files = null;
            files = dirInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                string fileFullName = file.FullName;
                long fileSize = file.Length;

                // Get age of file in days
                int age = GetFileAge(file);

                // Match file on RegularExpression filter
                if (optFilter)
                {
                    if (!Regex.IsMatch(file.Name.ToLower(), filterRegEx)) continue;
                }

                // Match file on RegularExpression exclude filter
                if (optExcludeFilter)
                {
                    if (Regex.IsMatch(file.Name.ToLower(), excludeFilterRegEx)) continue;
                }

                // Check if file is too old
                if (age >= maxAge)
                {
                    try
                    {
                        // Delete file if not simulating
                        if (!optSimulate)
                        {
                            file.IsReadOnly = false;
                            file.Delete();
                        }
                        Output(" Deleted file: {0} Age({1}) Size({2})", fileFullName.PadRight(40), age, FormatSize(fileSize));
                        totalSize += fileSize;
                        totalFiles++;
                    }
                    catch (Exception e)
                    {
                        Output(" Error: No access to file {0}. ({1})", file, e.Message.ToString());
                        totalErrors++;
                        continue;
                    }
                }
            }

            // Delete directory if option /D is supplied and directory is at level (depth) optDeleteEmptyLevel or deeper and directory is empty
            try
            {
                if (optDeleteEmpty && depth >= optDeleteEmptyLevel && Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    // Delete directory if not simulating
                    if (!optSimulate) Directory.Delete(directory);
                    totalDirectories++;
                    Output(" Deleted dir : {0}", directory);
                }
            }
            catch (Exception e)
            {
                Output(" Error: No access to directory {0}. ({1})", directory, e.Message.ToString());
                totalErrors++;
            }

        }

        static int GetFileAge(FileInfo file)
        {
            // Get the file age in days (youngest of modified- and created date)
            TimeSpan ageModified, ageCreated;
            ageModified = DateTime.Today - file.LastWriteTime;
            ageCreated = DateTime.Today - file.CreationTime;

            if (ageModified < ageCreated) return ageModified.Days;
            else return ageCreated.Days;
        }

        static string FormatSize(long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            while (size >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                size = size / 1024;
            }
            return String.Format("{0:0.##} {1}", size, sizes[order]);
        }

        static void ShowUsage()
        {
            string usage;
            usage = Environment.NewLine;
            usage += @"------------------------------------------------------------------------------" + Environment.NewLine;
            usage += @"   Cleanup v1.5  -  Delete old files and directories - TechnologySolutions" + Environment.NewLine;
            usage += @"------------------------------------------------------------------------------" + Environment.NewLine;
            usage += Environment.NewLine;
            usage += @"         Usage  :  Cleanup directory age [options]" + Environment.NewLine;
            usage += Environment.NewLine;
            usage += @"     directory  :  Directory to cleanup" + Environment.NewLine;
            usage += @"                   Like: drive:\path or \\server\share\path" + Environment.NewLine;
            usage += @"           age  :  Delete files older than .. days" + Environment.NewLine;
            usage += @"                   Looking at youngest date modified/created" + Environment.NewLine;
            usage += @"                   0 will delete all files" + Environment.NewLine;
            usage += Environment.NewLine;
            usage += @"     /F:filter  :  Only delete files which match this filter" + Environment.NewLine;
            usage += @"                   Like: /F:*.abc or /F:filename.ab?" + Environment.NewLine;
            usage += @"    /EF:filter  :  Exclude files which match this filter" + Environment.NewLine;
            usage += @"                   Like: /EF:keep.me or /EF:*.doc" + Environment.NewLine;
            usage += @"            /R  :  Recurse subdirecotries" + Environment.NewLine;
            usage += @"            /D  :  Delete empty subdirectories (same as: /D:1)" + Environment.NewLine;
            usage += @"      /D:level  :  Only delete empty directories from .. level and below" + Environment.NewLine;
            usage += @"                   Like: Cleanup.exe c:\rootdir 7 /r /d:2" + Environment.NewLine;
            usage += @"                   Will remove empty directory c:\rootdir\level1\level2 but not directory level1" + Environment.NewLine;
            usage += @"          /SIM  :  Simulate, don't delete anything" + Environment.NewLine;
            usage += @"          /LOG  :  Write output to screen and to Cleanup.log" + Environment.NewLine;
            usage += @"  /LOG:logfile  :  Write output to screen and to logfile" + Environment.NewLine;
            usage += @"                   Like: /LOG:cleanup.log or /LOG:""C:\Log Files\cleanup.log""" + Environment.NewLine;
            usage += Environment.NewLine;
            usage += @".cleanupignore  :  A directory can be ignored by placing a file named .cleanupignore in it" + Environment.NewLine;
            usage += Environment.NewLine;
            Console.WriteLine(usage);
        }

        static void Output(string text, params object[] args)
        {
            Console.WriteLine(text, args);
            Log(text, args);
        }

        static void Log(string text, params object[] args)
        {
            logFile.WriteLine(DateTime.Now + " " + text, args);
        }
    }
}
