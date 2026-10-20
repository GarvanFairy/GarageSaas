using System.Threading;
using System.Threading.Tasks;
using GarageSaas.Services.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IQuoteSuggestionService
    {
        Task<QuoteSuggestionResult> SuggestAsync(string draft, CancellationToken cancellationToken);
    }
}
