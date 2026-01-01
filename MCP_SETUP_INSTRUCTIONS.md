# MCP Server Configuration Summary

## Changes Made

### 1. Fixed Qdrant MCP Server
**Issues Fixed:**
- Changed command from `npx` to `uvx` (correct package manager for Python-based MCP servers)
- Updated auto-approve tools from `store_memory`, `search_memory` to `qdrant-store`, `qdrant-find` (correct tool names)

**Configuration:**
```json
"qdrant": {
  "command": "uvx",
  "args": ["mcp-server-qdrant"],
  "env": {
    "QDRANT_URL": "http://192.168.1.85:6333",
    "COLLECTION_NAME": "agent_memory",
    "EMBEDDING_MODEL": "sentence-transformers/all-MiniLM-L6-v2"
  }
}
```

### 2. Added Codacy MCP Server
**Configuration:**
```json
"codacy": {
  "command": "npx",
  "args": ["-y", "@codacy/codacy-mcp"],
  "env": {
    "CODACY_ACCOUNT_TOKEN": ""
  }
}
```

## Next Steps

### To Enable Codacy MCP Server:

1. **Get Your Codacy Account Token:**
   - Go to https://app.codacy.com/account/access-management
   - Click "Create API Token" or use an existing one
   - Copy the token

2. **Add the Token to Configuration:**
   - Open: `C:\Users\miha\AppData\Roaming\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`
   - Replace `"CODACY_ACCOUNT_TOKEN": ""` with your actual token
   - Example: `"CODACY_ACCOUNT_TOKEN": "your-token-here"`

3. **Restart Cline/VS Code:**
   - Close and reopen VS Code to load the new MCP servers

### Verify Installation:

After restarting, you should see these MCP servers available:

**Qdrant Tools:**
- `qdrant-store` - Store information with semantic search
- `qdrant-find` - Find relevant information using semantic search

**Codacy Tools:**
- `codacy_setup_repository` - Add/follow repositories
- `codacy_list_organizations` - List organizations
- `codacy_get_repository_with_analysis` - Get repository analysis
- `codacy_list_repository_issues` - List code quality issues
- `codacy_search_organization_srm_items` - Search security items
- `codacy_cli_analyze` - Run local analysis with Codacy CLI
- And many more...

## Troubleshooting

### If Qdrant doesn't work:
1. Ensure `uvx` is installed: `pip install uv`
2. Check if Qdrant server is running at `http://192.168.1.85:6333`
3. Test connection: `curl http://192.168.1.85:6333/collections`

### If Codacy doesn't work:
1. Verify your API token is valid
2. Ensure you have access to the Codacy account
3. Check that npx can access @codacy/codacy-mcp package

## Additional Resources

- **Codacy MCP Server:** https://github.com/codacy/codacy-mcp
- **Qdrant MCP Server:** https://github.com/qdrant/mcp-server-qdrant
- **MCP Documentation:** https://modelcontextprotocol.io/
