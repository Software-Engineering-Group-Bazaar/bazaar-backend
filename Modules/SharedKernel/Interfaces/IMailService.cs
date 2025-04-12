using SharedKernel.Models;

namespace SharedKernel.Interfaces
{
    public interface IMailService
    {
        bool SendMail(MailData mailData);
        Task<bool> SendMailAsync(MailData mailData);
        // bool SendHTMLMail(HTMLMailData htmlMailData);
        // bool SendRezervacijaMail(HTMLMailData htmlMailData);
        // bool SendNotifikacijaMail(NotifikacijaMailData htmlMailData);
    }
}