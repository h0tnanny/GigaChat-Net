# GigaChat.SemanticKernel.Example

This example shows several ways to use `GigaChat.Net.SemanticKernel`:

- `IChatCompletionService` with `ChatHistory` and `GigaChatPromptExecutionSettings`;
- streaming through Semantic Kernel;
- `ChatCompletionAgent`;
- direct `GigaChat.Net` SDK calls for models, token count, and embeddings.

## Configuration

Set one authentication variable:

```bash
export GIGACHAT_CREDENTIALS="<authorization-key>"
# or
export GIGACHAT_ACCESS_TOKEN="<access-token>"
```

Optional variables:

```bash
export GIGACHAT_MODEL="GigaChat"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"
export GIGACHAT_CA_BUNDLE_FILE="/path/to/russian_trusted_root_ca.cer"
export GIGACHAT_MAX_RETRIES="3"
export GIGACHAT_RETRY_BACKOFF_FACTOR="0.5"
export GIGACHAT_REASONING_EFFORT="low"
export GIGACHAT_CLIENT_ID="client-id"
export GIGACHAT_SESSION_ID="session-id"
export GIGACHAT_TRACE_ID="trace-id"
```

## Run

```bash
dotnet run --project examples/GigaChat.SemanticKernel.Example/GigaChat.SemanticKernel.Example.csproj -- "Составь чеклист релиза SDK"
```
