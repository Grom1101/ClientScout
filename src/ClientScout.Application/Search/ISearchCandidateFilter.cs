using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public interface ISearchCandidateFilter
{
    PrefilterResult Evaluate(string rawText, SearchSettings settings);
}
