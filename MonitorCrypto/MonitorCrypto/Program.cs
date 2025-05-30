using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static readonly List<string> criptomoedas = new List<string>
    {
        "BTC", "ETH", "LTC", "BCH", "XRP",
        "ADA", "DOT", "LINK", "XLM", "DOGE"
    };

    static readonly ConcurrentDictionary<string, decimal> precosAnteriores = new();

    static async Task Main()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _ = MonitorarTeclaEscAsync(cts);

        while (!token.IsCancellationRequested)
        {
            var tarefas = criptomoedas.Select(simbolo => ObterEConverterCotacaoAsync(simbolo, token));

            try
            {
                await Task.WhenAll(tarefas);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }

            Console.WriteLine($"\nAtualizado em: {DateTime.Now:T}\n");
            await Task.Delay(3000, token); // Aguarda 30 segundos
        }
    }

    static HttpClient CriarClienteHttp()
    {
        var cliente = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        cliente.DefaultRequestHeaders.Add("User-Agent", "MonitorCripto/1.0");
        cliente.DefaultRequestHeaders.Add("Accept", "application/json");
        return cliente;
    }

    static async Task ObterEConverterCotacaoAsync(string simbolo, CancellationToken token)
    {
        try
        {
            using var clienteHttp = CriarClienteHttp();
            var url = $"https://api.exchange.cryptomkt.com/api/3/public/price/rate?from={simbolo}&to=USDT";
            var resposta = await clienteHttp.GetAsync(url, token);
            resposta.EnsureSuccessStatusCode();

            var json = await resposta.Content.ReadAsStringAsync(token);
            using var documento = JsonDocument.Parse(json);

            if (documento.RootElement.TryGetProperty(simbolo, out var dadosMoeda))
            {
                var precoString = dadosMoeda.GetProperty("price").GetString();
                decimal precoAtual = decimal.Parse(precoString, CultureInfo.InvariantCulture);

                precosAnteriores.TryGetValue(simbolo, out decimal precoAnterior);
                precosAnteriores[simbolo] = precoAtual;

                ExibirResultadosNoConsole(simbolo, precoAtual, precoAnterior);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter dados de {simbolo}: {ex.Message}");
        }
    }

    static void ExibirResultadosNoConsole(string simbolo, decimal precoAtual, decimal precoAnterior)
    {
        var corOriginal = Console.ForegroundColor;
        Console.ForegroundColor = precoAtual > precoAnterior ? ConsoleColor.Green : ConsoleColor.Red;
        string variacao = precoAtual > precoAnterior ? "↑" : precoAtual < precoAnterior ? "↓" : "→";
        Console.WriteLine($"{simbolo}: ${precoAtual:N2} {variacao}");
        Console.ForegroundColor = corOriginal;
    }

    static async Task MonitorarTeclaEscAsync(CancellationTokenSource cts)
    {
        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                cts.Cancel();
                break;
            }
            await Task.Delay(100);
        }
    }
}
