Cleanup
=======

Console tool for cleaning up old files in a directory tree

-------------------------------------------------------------------------------
   Cleanup v1.1  -  Delete old files and directories - TechnologySolutions©
-------------------------------------------------------------------------------

         Usage  :  Cleanup directory age [options]


     directory  :  Directory to cleanup.
                   Like: drive:\path or \\server\share\path

           age  :  Delete files older than .. days.
                   Looking at youngest date modified/created.
                   0 will delete all files.


            /R  :  Recurse subdirecotries
            /D  :  Delete empty subdirectories
          /SIM  :  Simulate, don't delete anything
          /LOG  :  Write output to screen and to Cleanup.log
  /LOG:logfile  :  Write output to screen and to logfile
                   Like: /LOG:cleanup.log or /LOG:"C:\Log Files\cleanup.log"
