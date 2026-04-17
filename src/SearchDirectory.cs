using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace PwrSearch
{
    [Cmdlet(VerbsCommon.Search, "Directory")]
    public class SearchDirectory : Cmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string[] SearchDirectories { get; set; }

        [Parameter(Mandatory = true)]
        public string[] ExcludeDirectories { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Pattern { get; set; }

        [Parameter]
        public SwitchParameter All { get; set; }

        [Parameter]
        public bool SubstringMatch { get; set; }

        private HashSet<string> excludeNames;
        private HashSet<string> excludeFullPaths;
        private SearchQueue queue;

        protected override void BeginProcessing()
        {
            BuildExcludeSets();

            PartQuery initialQuery = PartQuery.Parse(this.Pattern, this.SubstringMatch);
            this.queue = new SearchQueue();
            foreach (string root in this.SearchDirectories)
            {
                WriteVerbose($"Search-Directory: adding root '{root}'");
                this.queue.Add(new SearchState(initialQuery, new DirectoryInfo(root), 0));
            }

            WriteVerbose($"Search-Directory: pattern='{this.Pattern}', substringMatch={this.SubstringMatch}, excludes={this.ExcludeDirectories?.Length ?? 0}");
            base.BeginProcessing();
        }

        private void BuildExcludeSets()
        {
            this.excludeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.excludeFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (this.ExcludeDirectories == null) { return; }

            foreach (string exclude in this.ExcludeDirectories)
            {
                if (string.IsNullOrEmpty(exclude)) { continue; }
                if (Path.IsPathRooted(exclude))
                {
                    this.excludeFullPaths.Add(Path.GetFullPath(exclude));
                }
                else
                {
                    this.excludeNames.Add(exclude);
                }
            }
        }

        private bool ShouldInclude(DirectoryInfo dir)
        {
            if (this.excludeNames.Count > 0 && this.excludeNames.Contains(dir.Name)) { return false; }
            if (this.excludeFullPaths.Count > 0 && this.excludeFullPaths.Contains(dir.FullName)) { return false; }
            return true;
        }

        protected override void ProcessRecord()
        {
            if (this.SearchDirectories.Length == 0 || string.IsNullOrEmpty(this.Pattern))
            {
                return;
            }

            while (!this.queue.IsEmpty && !this.Stopping)
            {
                List<SearchState> states = this.queue.DequeueNextSearchStateList();
                if (states == null) { break; }

                for (int i = 0; i < states.Count; i++)
                {
                    if (this.Stopping) { return; }
                    if (!AdvanceAndEmit(states[i])) { return; }
                }
            }
        }

        // Returns false once the caller should stop the search (first match found without -All).
        private bool AdvanceAndEmit(SearchState state)
        {
            IEnumerable<DirectoryInfo> directories;
            try
            {
                directories = state.Directory.EnumerateDirectories();
            }
            catch (UnauthorizedAccessException) { return true; }
            catch (DirectoryNotFoundException) { return true; }

            PartQuery query = state.Query;
            PartQuery next = query.Next;

            foreach (DirectoryInfo child in directories)
            {
                if (this.Stopping) { return true; }
                if (!ShouldInclude(child)) { continue; }

                if (query.Matches(child.Name))
                {
                    if (next == null)
                    {
                        WriteVerbose($"Search-Directory: match '{child.FullName}'");
                        WriteObject(child);
                        if (!this.All.IsPresent) { return false; }
                    }
                    else
                    {
                        int strength = StringComparer.OrdinalIgnoreCase.Equals(query.QueryString, child.Name) ? 2 : 1;
                        this.queue.Add(new SearchState(next, child, strength));
                    }
                }
                else
                {
                    this.queue.Add(new SearchState(query, child, 0));
                }
            }

            return true;
        }

        private sealed class SearchState
        {
            public readonly PartQuery Query;
            public readonly DirectoryInfo Directory;
            public readonly int Strength;

            public SearchState(PartQuery query, DirectoryInfo directory, int strength)
            {
                this.Query = query;
                this.Directory = directory;
                this.Strength = strength;
            }

            public int Depth { get { return this.Query.Depth; } }
        }

        private sealed class SearchQueue
        {
            private readonly List<List<SearchState>> depthBuckets = new List<List<SearchState>>();
            private int count;

            public bool IsEmpty { get { return this.count == 0; } }

            public void Add(SearchState state)
            {
                int depth = state.Depth;
                while (this.depthBuckets.Count <= depth)
                {
                    this.depthBuckets.Add(new List<SearchState>());
                }
                this.depthBuckets[depth].Add(state);
                this.count++;
            }

            public List<SearchState> DequeueNextSearchStateList()
            {
                for (int i = this.depthBuckets.Count - 1; i >= 0; i--)
                {
                    List<SearchState> bucket = this.depthBuckets[i];
                    if (bucket.Count > 0)
                    {
                        this.depthBuckets[i] = new List<SearchState>();
                        this.count -= bucket.Count;
                        if (bucket.Count > 1)
                        {
                            bucket.Sort(StrengthComparer.Instance);
                        }
                        return bucket;
                    }
                }
                return null;
            }

            private sealed class StrengthComparer : IComparer<SearchState>
            {
                public static readonly StrengthComparer Instance = new StrengthComparer();
                public int Compare(SearchState x, SearchState y) { return y.Strength.CompareTo(x.Strength); }
            }
        }

        private sealed class PartQuery
        {
            public readonly Predicate<string> Matches;
            public readonly string QueryString;
            public readonly PartQuery Next;
            public readonly int Depth;

            private PartQuery(string queryString, Predicate<string> matches, int depth, PartQuery next)
            {
                this.QueryString = queryString;
                this.Matches = matches;
                this.Depth = depth;
                this.Next = next;
            }

            public static PartQuery Parse(string pattern, bool substringMatch)
            {
                string[] parts = pattern.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                PartQuery next = null;
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    next = new PartQuery(parts[i], CreateMatcher(parts[i], substringMatch), i, next);
                }
                return next;
            }

            private static Predicate<string> CreateMatcher(string matchString, bool substringMatch)
            {
                if (matchString.IndexOf('*') >= 0)
                {
                    string escaped = Regex.Escape(matchString).Replace("\\*", ".*");
                    string regex = substringMatch
                        ? "^.*" + escaped + ".*$"
                        : "^" + escaped + ".*$";
                    Regex re = new Regex(regex, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
                    return s => re.IsMatch(s);
                }

                string needle = matchString;
                if (substringMatch)
                {
                    return s => s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return s => s.StartsWith(needle, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
