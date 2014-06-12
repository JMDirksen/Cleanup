using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        static bool optRecurse = false;
        static bool optSimulate = false;
        static bool optLog = false;
        static string logFilename = null;
        static StreamWriter logFile;
        static string rootDirectory;
        static bool optFilter = false;
        static string filter;
        static string filterRegEx;

        static void Main(string[] args)
        {
            string parameters = "";

            //Get arguments from command-line
            try
            {
                rootDirectory = args[0];
                maxAge = int.Parse(args[1]);
                foreach (string x in args)
                {
                    parameters += x + " ";
                    if (x.ToUpper().Equals("/D")) optDeleteEmpty = true;
                    else if (x.ToUpper().Equals("/R")) optRecurse = true;
                    else if (x.ToUpper().Equals("/SIM")) optSimulate = true;
                    else if (x.ToUpper().StartsWith("/LOG"))
                    {
                        optLog = true;
                        if ( x.ToUpper().StartsWith("/LOG:") ) logFilename = x.Substring(5);
                        else logFilename = "Cleanup.log";
                    }
                    else if (x.ToUpper().StartsWith("/F:"))
                    {
                        optFilter = true;
                        filter = x.Substring(3);
                        filterRegEx = Regex.Escape(filter);
                        filterRegEx = "^" + filterRegEx.Replace("\\*", ".+").Replace("\\?", ".") + "$";
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
                //Show Usage when something wrong/missing in arguments
                ShowUsage();
                return;
            }

            //Check if direcotry exists
            if (!Directory.Exists(rootDirectory))
            {
                Console.WriteLine("Directory {0} does not exist.\n", rootDirectory);
                return;
            }
            
            //Open and check if logfile is writable
            if(optLog)
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

            //Go do stuff
            Output("Cleanup started with parameters: {0}",parameters);
            CleanupDirectory(rootDirectory);

            //Show summary
            if (optSimulate) Output("\nWould have deleted {0} files with a total size of {1}.", totalFiles, FormatSize(totalSize));
            else Output("\nDeleted {0} files ({1}) and {2} directories, encountered {3} errors.", totalFiles, FormatSize(totalSize), totalDirectories, totalErrors);

            logFile.Close();
        }

        static void CleanupDirectory(string directory)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directory);
            DirectoryInfo[] dirs = dirInfo.GetDirectories();
            
            foreach (DirectoryInfo dir in dirs)
            {
                //Recurse subdirectories
                if(optRecurse) CleanupDirectory(dir.FullName);
            }

            
            //Process files in directory
            //Output("Processing: " + directory);
            FileInfo[] files = null;
            files = dirInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                string fileFullName = file.FullName;
                long fileSize = file.Length;

                //Get age of file in days
                int age = GetFileAge(file);

                //Match file on RegularExpression filter
                if (optFilter)
                {
                    if(!Regex.IsMatch(file.Name,filterRegEx)) continue;
                }

                //Check if file is too old
                if (age >= maxAge)
                {
                    try
                    {
                        //Delete file if not simulating
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
                
            
            //Delete directory if option /D is supplied, not the root folder and is empty
            try
            {
                if (optDeleteEmpty && directory != rootDirectory && Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    //Delete directory if not simulating
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
            //Get the file age in days (youngest of modified- and created date)
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
            usage = "\n";
            usage += "-------------------------------------------------------------------------------\n";
            usage += "   Cleanup v1.2  -  Delete old files and directories - TechnologySolutions©\n";
            usage += "-------------------------------------------------------------------------------\n";
            usage += "\n";
            usage += "         Usage  :  Cleanup directory age [options]\n";
            usage += "\n";
            usage += "\n";
            usage += "     directory  :  Directory to cleanup.\n";
            usage += "                   Like: drive:\\path or \\\\server\\share\\path\n";
            usage += "\n";
            usage += "           age  :  Delete files older than .. days.\n";
            usage += "                   Looking at youngest date modified/created.\n";
            usage += "                   0 will delete all files.\n";
            usage += "\n";
            usage += "\n";
            usage += "     /F:filter  :  Only delete files which match this filter\n";
            usage += "                   Like: /F:*.abc or /F:filename.ab?\n";
            usage += "            /R  :  Recurse subdirecotries\n";
            usage += "            /D  :  Delete empty subdirectories\n";
            usage += "          /SIM  :  Simulate, don't delete anything\n";
            usage += "          /LOG  :  Write output to screen and to Cleanup.log\n";
            usage += "  /LOG:logfile  :  Write output to screen and to logfile\n";
            usage += "                   Like: /LOG:cleanup.log or /LOG:\"C:\\Log Files\\cleanup.log\"\n";
            usage += "\n";
            Console.WriteLine(usage);
        }

        static void Output(string text, params object[] args)
        {
            Console.WriteLine(text,args);
            Log(text,args);
        }

        static void Log(string text, params object[] args)
        {
            logFile.WriteLine(DateTime.Now+" "+text,args);
        }
    }
}
