using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace NotionClientLib;

/// <summary>
/// Provides methods for interacting with the Notion API, enabling retrieval and manipulation of pages and their
/// content.
/// </summary>
/// <remarks>The NotionClient class requires an API key and a Notion API version to be specified during
/// initialization. It manages HTTP requests to the Notion API and handles authentication through request headers.
/// Ensure that the API key supplied has the necessary permissions to access and modify the targeted pages. All
/// operations are performed asynchronously and may throw exceptions if the underlying HTTP requests fail or if invalid
/// parameters are provided.</remarks>
public class NotionClient
{
    private readonly HttpClient _http;
    private const string BASE_URL = "https://api.notion.com/v1";
    private readonly JsonSerializerOptions options = new()   // "object_" → "object" にシリアライズ時に変換が必要
    {
        PropertyNamingPolicy = new ObjectUnderscorePolicy()
    };

    public NotionClient(string apiKey, string version)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.Add("Notion-Version", version);
    }

    // ページのブロック（中身）を取得
    /// <summary>
    /// Asynchronously retrieves the plain text content of all blocks contained within the specified page.
    /// </summary>
    /// <remarks>This method sends an asynchronous HTTP GET request to fetch the child blocks of the specified
    /// page. Only the plain text from each block's rich text content is included in the result. The method does not
    /// return any formatting or metadata associated with the blocks.</remarks>
    /// <param name="pageId">The unique identifier of the page whose block content is to be retrieved. This parameter cannot be null or
    /// empty.</param>
    /// <returns>A string containing the concatenated plain text of all blocks in the specified page. Returns "（内容なし）" if the
    /// page contains no content blocks.</returns>
    public async Task<string> GetBlocksAsync(string pageId)
    {
        var url = $"{BASE_URL}/blocks/{pageId}/children";
        var response = await _http.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"取得失敗: {response.StatusCode} / {json}\nURL: {url}";

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

    // ページにテキストを追記
    /// <summary>
    /// Asynchronously appends the specified text as a new paragraph block to the content of the page identified by its
    /// unique ID.
    /// </summary>
    /// <remarks>This method sends a PATCH request to the Notion API to add a new paragraph block containing
    /// the specified text to the target page. Ensure that the page ID is valid and that the text is properly formatted.
    /// The operation will fail if the page does not exist or if the API returns an error.</remarks>
    /// <param name="pageId">The unique identifier of the page to which the text will be appended. This parameter cannot be null or empty.</param>
    /// <param name="text">The text content to append to the page. This parameter cannot be null.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    /// <exception cref="Exception">Thrown if the HTTP request to append the text fails.</exception>
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

        var json = JsonSerializer.Serialize(body, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync($"{BASE_URL}/blocks/{pageId}/children", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"書き込み失敗: {error}");
        }
    }

    // 子ページを作成
    /// <summary>
    /// Creates a new child page under the specified parent page with the given title and body content.
    /// </summary>
    /// <remarks>This method sends a request to the Notion API to create a new page as a child of the
    /// specified parent. The parent page must exist and the caller must have permission to create pages under
    /// it.</remarks>
    /// <param name="parentPageId">The unique identifier of the parent page under which the new child page will be created. Cannot be null or
    /// empty.</param>
    /// <param name="title">The title to assign to the new child page. Cannot be null or empty.</param>
    /// <param name="bodyText">The body content to include in the new child page. Cannot be null; may be empty for a blank page.</param>
    /// <returns>A string containing the unique identifier of the newly created child page.</returns>
    /// <exception cref="Exception">Thrown if the page creation fails due to an unsuccessful HTTP response from the server.</exception>
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

        var json = JsonSerializer.Serialize(body, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{BASE_URL}/pages", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"ページ作成失敗: {responseJson}");

        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    // 子ページ一覧取得
    /// <summary>
    /// Asynchronously retrieves a list of child pages for the specified parent page.
    /// </summary>
    /// <remarks>Only pages of type 'child_page' are included in the returned list. The method issues an HTTP
    /// GET request to retrieve the child pages from the specified parent page.</remarks>
    /// <param name="pageId">The unique identifier of the parent page whose child pages are to be retrieved. This parameter cannot be null or
    /// empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of tuples, each consisting of
    /// the unique identifier and title of each child page.</returns>
    /// <exception cref="Exception">Thrown if the HTTP request fails or if the response indicates an error. The exception message contains details
    /// about the failure.</exception>
    public async Task<List<(string Id, string Title)>> GetChildPageListAsync(string pageId)
    {
        var response = await _http.GetAsync($"{BASE_URL}/blocks/{pageId}/children");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"取得失敗: {json}");

        var doc = JsonDocument.Parse(json);
        var pages = new List<(string Id, string Title)>();

        foreach (var block in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type != "child_page") continue;

            var id = block.GetProperty("id").GetString()!;
            var title = block
                .GetProperty("child_page")
                .GetProperty("title")
                .GetString()!;

            pages.Add((id, title));
        }

        return pages;
    }

}


// "object_" を "object" に変換するカスタムポリシー
public class ObjectUnderscorePolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name == "object_" ? "object" : name;
}