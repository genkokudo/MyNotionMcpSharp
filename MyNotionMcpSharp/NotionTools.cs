using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotionClientLib;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace MyNotionMcpSharp;

public class NotionTools
{
    private IConfigurationRoot _config;
    private string _apiKey;
    private string _pageId;
    private readonly NotionClient _notion;
    private readonly ILogger<NotionTools> _logger;
    private const string NOTION_VERSION = "2022-06-28";

    public NotionTools(ILogger<NotionTools> logger)
    {
        _logger = logger;
        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)        // 使わない
            .AddJsonFile("appsettings.local.json", optional: true)  // 使わない
            .AddEnvironmentVariables()  // 環境変数から読み込み
            .Build();
        _apiKey = _config["Notion:ApiKey"]!;
        _pageId = _config["Notion:PageId"]!;
        _notion = new NotionClient(_apiKey, NOTION_VERSION);
    }

    // ===== Notion: ページ読み取り =====
    [Function(nameof(GetNotionPage))]
    public async Task<string> GetNotionPage(
        [McpToolTrigger(nameof(GetNotionPage), "サンドボックスページの内容を読み取ります。")]
            ToolInvocationContext context)
    {
        _logger.LogInformation("GetNotionPage: {pageId}", _pageId);
        return await _notion.GetBlocksAsync(_pageId);
    }

    // ===== Notion: テキスト追記 =====
    [Function(nameof(AppendToNotionPage))]
    public async Task<string> AppendToNotionPage(
        [McpToolTrigger(nameof(AppendToNotionPage), "サンドボックスページにテキストを追記します。")]
            ToolInvocationContext context,
        [McpToolProperty(nameof(text), "追記するテキスト", true)]
            string text)
    {
        _logger.LogInformation("AppendToNotionPage: {text}", text);
        await _notion.AppendTextAsync(_pageId, text);
        return "書き込み完了！";
    }

    // ===== Notion: 子ページ作成 =====
    [Function(nameof(CreateNotionChildPage))]
    public async Task<string> CreateNotionChildPage(
        [McpToolTrigger(nameof(CreateNotionChildPage), "サンドボックス配下に子ページを作成します。")]
            ToolInvocationContext context,
        [McpToolProperty(nameof(title), "作成するページのタイトル", true)]
            string title,
        [McpToolProperty(nameof(body), "作成するページの本文", true)]
            string body)
    {
        _logger.LogInformation("CreateNotionChildPage: {title}", title);
        var newPageId = await _notion.CreateChildPageAsync(_pageId, title, body);
        return $"作成完了！ページID: {newPageId}";
    }

    // ===== Notion: 子ページ一覧取得 =====
    [Function(nameof(GetChildPageList))]
    public async Task<string> GetChildPageList(
        [McpToolTrigger(nameof(GetChildPageList), "サンドボックス配下のページ一覧を取得します。")]
        ToolInvocationContext context)
    {
        _logger.LogInformation("GetChildPageList: {pageId}", _pageId);
        var pages = await _notion.GetChildPageListAsync(_pageId);

        if (!pages.Any())
            return "子ページなし";

        var sb = new StringBuilder();
        foreach (var (id, title) in pages)
            sb.AppendLine($"{title}: {id}");

        return sb.ToString();
    }

    //// ===== Notion: 指定ページの内容取得 =====
    //[Function(nameof(GetChildPage))]
    //public async Task<string> GetChildPage(
    //    [McpToolTrigger(nameof(GetChildPage), "指定したページIDの内容を取得します。")]
    //    ToolInvocationContext context,
    //    [McpToolProperty(nameof(text), "取得するページのID", true)]
    //    string text)
    //{
    //    _logger.LogInformation("GetChildPage: {pageId}", text);
    //    if (string.IsNullOrEmpty(text))
    //        return $"エラー: pageIdが空です。context={System.Text.Json.JsonSerializer.Serialize(context)}";
    //    return await _notion.GetBlocksAsync(text);  // これで試してみる
    //}
    // ===== Notion: 指定ページの内容取得 =====
    [Function(nameof(GetChildPage))]
    public async Task<string> GetChildPage(
        [McpToolTrigger(nameof(GetChildPage), "指定したページIDの内容を取得します。")]
    ToolInvocationContext context,
        [McpToolProperty("pageId", "取得するページのID", true)] // ← nameofやなく文字列リテラルで！
    string pageId)
    {
        _logger.LogInformation("GetChildPage: {pageId}", pageId);
        if (string.IsNullOrEmpty(pageId))
            return $"エラー: pageIdが空です。context={System.Text.Json.JsonSerializer.Serialize(context)}";
        return await _notion.GetBlocksAsync(pageId);
    }
}