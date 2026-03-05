using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotionClientLib;
using System.Security.Cryptography;
using System.Text;

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
        [McpToolProperty(nameof(text), "追記するテキスト")]
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
        [McpToolProperty(nameof(title), "作成するページのタイトル")]
            string title,
        [McpToolProperty(nameof(body), "作成するページの本文")]
            string body)
    {
        _logger.LogInformation("CreateNotionChildPage: {title}", title);
        var newPageId = await _notion.CreateChildPageAsync(_pageId, title, body);
        return $"作成完了！ページID: {newPageId}";
    }

}