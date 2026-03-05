// =============================
// NotionClient クラス
// =============================
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class NotionClient
{
    private readonly HttpClient _http;
    private const string BASE_URL = "https://api.notion.com/v1";

    public NotionClient(string apiKey, string version)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.Add("Notion-Version", version);
    }

    // ① ページのブロック（中身）を取得
    public async Task<string> GetBlocksAsync(string pageId)
    {
        var response = await _http.GetAsync($"{BASE_URL}/blocks/{pageId}/children");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"取得失敗: {json}");

        // テキストだけ抜き出す
        var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();

        foreach (var block in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (block.TryGetProperty(type!, out var content) &&
                content.TryGetProperty("rich_text", out var richText))
            {
                foreach (var text in richText.EnumerateArray())
                {
                    sb.AppendLine(text.GetProperty("plain_text").GetString());
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : "（内容なし）";
    }

    // ② ページにテキストを追記
    public async Task AppendTextAsync(string pageId, string text)
    {
        var body = new
        {
            children = new[]
            {
                new
                {
                    object_ = "block",
                    type = "paragraph",
                    paragraph = new
                    {
                        rich_text = new[]
                        {
                            new { type = "text", text = new { content = text } }
                        }
                    }
                }
            }
        };

        // "object_" → "object" にシリアライズ時に変換が必要
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new ObjectUnderscorePolicy()
        };

        var json = JsonSerializer.Serialize(body, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync($"{BASE_URL}/blocks/{pageId}/children", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"書き込み失敗: {error}");
        }
    }

    // ③ 子ページを作成
    public async Task<string> CreateChildPageAsync(string parentPageId, string title, string bodyText)
    {
        var body = new
        {
            parent = new { page_id = parentPageId },
            properties = new
            {
                title = new
                {
                    title = new[]
                    {
                        new { type = "text", text = new { content = title } }
                    }
                }
            },
            children = new[]
            {
                new
                {
                    object_ = "block",
                    type = "paragraph",
                    paragraph = new
                    {
                        rich_text = new[]
                        {
                            new { type = "text", text = new { content = bodyText } }
                        }
                    }
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new ObjectUnderscorePolicy()
        };

        var json = JsonSerializer.Serialize(body, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{BASE_URL}/pages", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"ページ作成失敗: {responseJson}");

        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("id").GetString()!;
    }
}

// "object_" を "object" に変換するカスタムポリシー
public class ObjectUnderscorePolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name == "object_" ? "object" : name;
}