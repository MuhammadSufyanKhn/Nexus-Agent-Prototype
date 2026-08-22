# Local Open-Source LLM Configuration

This directory contains configurations, prompt templates, and connection settings for the local open-source LLM runtime.

## Supported Runtimes
- **Ollama** (Default endpoint: `http://localhost:11434`)
- **LocalAI** / **LM Studio** (OpenAI-compatible local endpoint: `http://localhost:1234/v1`)

## Recommended Models
- `llama3.2` / `llama3:8b`
- `mistral:7b`
- `qwen2.5-coder:7b`

## Setup Instructions
1. Install Ollama from https://ollama.com
2. Pull your local model:
   ```bash
   ollama pull llama3.2
   ```
3. Ensure the local runtime server is running before executing agent prompts.
