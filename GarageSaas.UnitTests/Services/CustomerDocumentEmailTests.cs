using System.Net;
using System.Net.Sockets;
using System.Text;
using GarageSaas.Configuration;
using GarageSaas.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class CustomerDocumentEmailTests
{
    [TestMethod]
    public async Task MailKitSendsPdfMimeAttachmentUsingExistingSmtpSettings()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var capture=Task.Run(async ()=>
        {
            using var socket=await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream=socket.GetStream();using var reader=new StreamReader(stream,Encoding.ASCII,false,4096,true);
            using var writer=new StreamWriter(stream,Encoding.ASCII,4096,true) { NewLine="\r\n",AutoFlush=true };
            await writer.WriteLineAsync("220 localhost test SMTP");
            string? message=null;
            while(true)
            {
                var line=await reader.ReadLineAsync(timeout.Token);if(line==null)break;
                if(line.StartsWith("EHLO")||line.StartsWith("HELO"))await writer.WriteLineAsync("250 localhost");
                else if(line.StartsWith("MAIL FROM:")||line.StartsWith("RCPT TO:"))await writer.WriteLineAsync("250 OK");
                else if(line=="DATA")
                {
                    await writer.WriteLineAsync("354 End with dot");var data=new StringBuilder();
                    while((line=await reader.ReadLineAsync(timeout.Token))!=null&&line!=".")data.AppendLine(line.StartsWith("..")?line[1..]:line);
                    message=data.ToString();await writer.WriteLineAsync("250 Accepted");
                }
                else if(line=="QUIT"){await writer.WriteLineAsync("221 Bye");break;}
                else await writer.WriteLineAsync("250 OK");
            }
            return message;
        },timeout.Token);
        var email=new MailKitEmailService(Options.Create(new EmailSettings { SmtpHost="127.0.0.1",SmtpPort=((IPEndPoint)listener.LocalEndpoint).Port,SenderName="Garage",SenderEmail="garage@example.invalid",UseStartTls=false }));
        var bytes=Encoding.ASCII.GetBytes("%PDF-synthetic attachment bytes");
        await email.SendDocumentAsync("customer@example.invalid","Quote Q-100","<p>Your quote is attached.</p>","Quote-100.pdf",bytes).WaitAsync(timeout.Token);
        var raw=await capture.WaitAsync(timeout.Token);Assert.IsNotNull(raw);
        using var input=new MemoryStream(Encoding.ASCII.GetBytes(raw!));var message=await MimeMessage.LoadAsync(input,timeout.Token);
        Assert.AreEqual("customer@example.invalid",message.To.Mailboxes.Single().Address);Assert.AreEqual("Quote Q-100",message.Subject);
        var attachment=(MimePart)message.Attachments.Single();Assert.AreEqual("application/pdf",attachment.ContentType.MimeType);Assert.AreEqual("Quote-100.pdf",attachment.FileName);
        using var output=new MemoryStream();await attachment.Content!.DecodeToAsync(output,timeout.Token);CollectionAssert.AreEqual(bytes,output.ToArray());
    }
}
