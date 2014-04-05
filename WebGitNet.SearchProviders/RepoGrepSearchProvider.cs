﻿namespace WebGitNet.SearchProviders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Search;

    public class RepoGrepSearchProvider : ISearchProvider
    {
        public Task<IList<SearchResult>> Search(SearchQuery query, FileManager fileManager, RepoInfo repository, int skip, int count)
        {
            if (repository != null)
            {
                return Task.Factory.StartNew(() => (IList<SearchResult>)Search(query, repository).Skip(skip).Take(count).ToList());
            }
            else
            {
                return Task.Factory.StartNew(() => (IList<SearchResult>)Search(query, fileManager).Skip(skip).Take(count).ToList());
            }
        }

        public IEnumerable<SearchResult> Search(SearchQuery query, FileManager fileManager)
        {
            var repos = from dir in fileManager.DirectoryInfo.EnumerateDirectories()
                        let repoInfo = GitUtilities.GetRepoInfo(dir.FullName)
                        where repoInfo.IsGitRepo
                        select repoInfo;

            return from repo in repos
                   from searchResult in Search(query, repo, includeRepoName: true)
                   select searchResult;
        }

        private IEnumerable<SearchResult> Search(SearchQuery query, RepoInfo repo, bool includeRepoName = false)
        {
            var allTerms = string.Join(" --or ", query.Terms.Select(t => "-e " + GitUtilities.Q(t)));
            var commandResult = GitUtilities.Execute("grep --line-number --fixed-strings --ignore-case --context 3 --null --all-match " + allTerms + " HEAD", repo.RepoPath);

            var repoResults = commandResult.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            return from m in repoResults
                   where m != "--"
                   where !m.StartsWith("Binary file")
                   let parts = m.Split('\0')
                   let filePath = parts[0].Split(':')[1]
                   let searchLine = new SearchLine
                   {
                       Line = parts[2],
                       LineNumber = int.Parse(parts[1]),
                   }
                   group searchLine by filePath into g
                   select new SearchResult
                   {
                       LinkText = (includeRepoName ? repo.Name + " " : string.Empty) + "/" + g.Key,
                       ActionName = "ViewBlob",
                       ControllerName = "Browse",
                       RouteValues = new { repo = repo.Name, @object = "HEAD", path = g.Key },
                       Lines = g.ToList(),
                   };
        }
    }
}
