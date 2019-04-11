using System.IO;
using System.Linq;
using static Build.Buildary.Shell;
using static Build.Buildary.Directory;
using static Build.Buildary.File;

namespace Build
{
    public class Windows64Platform : IPlatform
    {
        public string QtVersion => "5.12.2";

        public string PlatformArch => "win-64";
        
        public string[] GetUrls()
        {
            var urls = Helpers.GetQtArchives(
                    "https://download.qt.io/online/qtsdkrepository/windows_x86/desktop/qt5_5122",
                    "qt.qt5.5122.win64_msvc2017_64",
                    "qt.qt5.5122.qtvirtualkeyboard.win64_msvc2017_64")
                .ToList();
            
            urls.AddRange(Helpers.GetQtArchives(
                "https://download.qt.io/online/qtsdkrepository/windows_x86/desktop/tools_qtcreator",
                "qt.tools.qtcreator"));

            return urls.ToArray();
        }

        private void Patch(string extractedDirectory)
        {
            RunShell($"mv \"{extractedDirectory}/{QtVersion}/msvc2017_64\" \"{extractedDirectory}/qt\"");
            DeleteDirectory($"{extractedDirectory}/{QtVersion}");
            using (var fileStream = File.OpenWrite(Path.Combine(extractedDirectory, "qt", "bin", "qt.conf")))
            {
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine("[Paths]");
                    streamWriter.WriteLine("Prefix=..");
                }
            }
            
            using(var fileStream = File.Open(Path.Combine(extractedDirectory, "qt", "mkspecs", "qconfig.pri"), FileMode.Append))
            {
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine("QT_EDITION = OpenSource");
                    streamWriter.WriteLine("QT_LICHECK =");
                }
            }
        }
        
        public void PackageDev(string extractedDirectory, string destination, string version)
        {
            Patch(extractedDirectory);
            File.WriteAllText(Path.Combine(extractedDirectory, "version.txt"), version);
            
            RunShell($"cd \"{extractedDirectory}\" && tar -cvzpf \"{destination}\" *");
        }

        public void PackageRuntime(string extractedDirectory, string destination, string version)
        {
            Patch(extractedDirectory);
            File.WriteAllText(Path.Combine(extractedDirectory, "version.txt"), version);
            
            DeleteDirectory(Path.Combine(extractedDirectory, "Tools"));
            foreach (var directory in GetDirecories(Path.Combine(extractedDirectory, "qt")))
            {
                switch (Path.GetFileName(directory))
                {
                    case "bin":
                        // Delete everything that isn't a .dll in here.
                        foreach (var file in GetFiles(directory))
                        {
                            if (Path.GetExtension(file) != ".dll")
                            {
                                DeleteFile(file);
                            }
                        }
                        break;
                    case "qml":
                    case "plugins":
                        break;
                    default:
                        DeleteDirectory(directory);
                        break;
                }
            }
            
            // The windows build currently brings in all the .dll's for packaging.
            // However, it also brings in the *d.dll/*.pdb files. Let's remove them.
            foreach(var file in GetFiles(Path.Combine(extractedDirectory, "qt"), recursive:true))
            {
                if (file.EndsWith("d.dll"))
                {
                    if(FileExists(file.Substring(0, file.Length - 5) + ".dll"))
                    {
                        // This is a debug dll.
                        DeleteFile(file);
                    }
                }
                else if (file.EndsWith(".pdb"))
                {
                    DeleteFile(file);
                }
                else if (file.EndsWith("*.qmlc"))
                {
                    DeleteFile(file);
                }
            }
            
            RunShell($"cd \"{extractedDirectory}\" && tar -cvzpf \"{destination}\" *");
        }
    }
}