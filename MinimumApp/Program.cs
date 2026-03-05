using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", optional: true) // ←上書きされる
    .Build();

// Notion API 疎通確認
const string NOTION_VERSION = "2022-06-28";
string notionApiKey = config["Notion:ApiKey"] ?? throw new Exception("Notion:ApiKeyが設定されていません");
string targetPageId = config["Notion:PageId"] ?? throw new Exception("Notion:PageIdが設定されていません");

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", notionApiKey);
client.DefaultRequestHeaders.Add("Notion-Version", NOTION_VERSION);

Console.WriteLine("🗻 Notion API 疎通確認開始...");

try
{
    // ページ取得
    var response = await client.GetAsync(
        $"https://api.notion.com/v1/pages/{targetPageId}");

    Console.WriteLine($"ステータス: {response.StatusCode}");

    var json = await response.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(json);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine("✅ 接続成功！");
        // タイトル取得
        var title = doc.RootElement
            .GetProperty("properties")
            .GetProperty("title")
            .GetProperty("title")[0]
            .GetProperty("plain_text")
            .GetString();
        Console.WriteLine($"ページ名: {title}");
    }
    else
    {
        Console.WriteLine("❌ エラー発生！");
        Console.WriteLine(json);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"💥 例外: {ex.Message}");
}

var notion = new NotionClient(notionApiKey, NOTION_VERSION);

// 動作確認
Console.WriteLine("=== ① ページの中身を読む ===");
var blocks = await notion.GetBlocksAsync(targetPageId);
Console.WriteLine(blocks);

Console.WriteLine("\n=== ② ページに書き込む ===");
await notion.AppendTextAsync(targetPageId, "俺からのテストメッセージや！🐔");
Console.WriteLine("書き込み完了！");

Console.WriteLine("\n=== ③ 子ページを作る ===");
var newPageId = await notion.CreateChildPageAsync(targetPageId, "テスト子ページ", "ここが本文や！");
Console.WriteLine($"作成した子ページID: {newPageId}");


