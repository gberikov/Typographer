using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Typographer.Oracle;

/// <summary>Клиент SOAP-сервиса typograf.artlebedev.ru.</summary>
/// <remarks>
/// Параметры повторяют tools/oracle-snapshot.sh: entityType=3 — символы, а не сущности;
/// useBr и useP выключены; maxNobr=0. Иначе живая сверка сравнивала бы снимок с выводом,
/// снятым при других настройках, и расходилась бы всегда.
/// Сервис чужой: между запросами выдерживается секунда.
/// </remarks>
public sealed class LebedevService : IDisposable
{
    private const string Endpoint = "https://typograf.artlebedev.ru/webservices/typograf.asmx";
    private const string Namespace = "http://typograf.artlebedev.ru/webservices/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Типографирует строку сервисом и возвращает результат.</summary>
    public async Task<string> ProcessTextAsync(string text, CancellationToken cancellationToken = default)
    {
        string body =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>"
            + $"<ProcessText xmlns=\"{Namespace}\">"
            + $"<text>{Escape(text)}</text>"
            + "<entityType>3</entityType><useBr>false</useBr><useP>false</useP><maxNobr>0</maxNobr>"
            + "</ProcessText></soap:Body></soap:Envelope>";

        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        request.Headers.Add("SOAPAction", $"\"{Namespace}ProcessText\"");

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        string xml = await response.Content.ReadAsStringAsync(cancellationToken);
        XElement result = XDocument.Parse(xml).Descendants()
            .First(e => e.Name.LocalName == "ProcessTextResult");

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return result.Value.Trim();
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static string Escape(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
