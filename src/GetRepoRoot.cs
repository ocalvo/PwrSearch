using System;
using System.IO;
using System.Management.Automation;

namespace DirHelper
{
    [Cmdlet(VerbsCommon.Get, "RepoRoot")]
    [OutputType(typeof(string))]
    public class GetRepoRoot : PSCmdlet
    {
        [Parameter(Position = 0)]
        public string Path { get; set; }

        [Parameter]
        public string Marker { get; set; }

        protected override void ProcessRecord()
        {
            string marker = string.IsNullOrEmpty(this.Marker) ? ".git" : this.Marker;

            string start = this.Path;
            if (string.IsNullOrEmpty(start))
            {
                start = this.SessionState.Path.CurrentFileSystemLocation.Path;
            }

            string dir;
            try
            {
                dir = System.IO.Path.GetFullPath(start);
            }
            catch (ArgumentException) { return; }
            catch (NotSupportedException) { return; }
            catch (PathTooLongException) { return; }

            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = System.IO.Path.Combine(dir, marker);
                if (Directory.Exists(candidate) || File.Exists(candidate))
                {
                    WriteObject(dir);
                    return;
                }

                string parent = System.IO.Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || StringComparer.OrdinalIgnoreCase.Equals(parent, dir))
                {
                    return;
                }
                dir = parent;
            }
        }
    }
}
