using GarageSaas.Services.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface ICustomerDocumentPdfRenderer
    {
        byte[] Generate(CustomerDocumentModel model);
    }
}
