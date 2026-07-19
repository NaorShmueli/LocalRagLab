$ErrorActionPreference = "Stop"

if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) {
    throw "Ollama is not installed or is not in PATH. Install it from https://ollama.com/download"
}

Write-Host "Pulling local chat model..."
ollama pull llama3.2:3b

Write-Host "Pulling multilingual embedding model..."
ollama pull nomic-embed-text-v2-moe

Write-Host "Installed models:"
ollama list

Write-Host "Ollama setup completed. Keep the Ollama application/service running."
