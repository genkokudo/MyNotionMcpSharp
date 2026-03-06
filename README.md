# MyNotionMcpSharp

## 概要
Azure Functions で Notion を操作する MCP サーバー

## 必要環境
- .NET 9
- Azure Functions Core Tools
- Notion Integration

## ローカル起動
func start

## 必要な環境変数
- NotionApiKey
- AllowedParentId

# 開発について
どうやらライブラリがバグっているようで、変数のバインドができず、関数に値が空で渡されてしまう。
最新のライブラリにして別プロジェクトでやり直し。
