# Local RAG Lab — C# / .NET 8 / Ollama

פרויקט לימודי מלא שמאפשר לדבג כל שלב במערכת RAG מקומית:

```text
Document/PDF
  -> local text extraction
  -> chunking
  -> local embedding through Ollama
  -> in-memory vector store
  -> cosine similarity
  -> transparent hybrid reranking
  -> grounded prompt construction
  -> local LLM through Ollama
  -> citations
  -> optional LLM-as-a-judge groundedness check
  -> full trace available through Swagger
```

אין שימוש ב־OpenAI, Anthropic, ענן או Vector DB חיצוני. כל המידע והמודלים נשארים במחשב המקומי.

## רכיבים

- **ASP.NET Core 8 Web API**
- **Swagger UI**
- **Ollama `/api/embed`** עבור embeddings
- **Ollama `/api/chat`** עבור LLM generation
- **`nomic-embed-text-v2-moe`** כברירת מחדל ל־embeddings רב־לשוניים
- **`llama3.2:3b`** כברירת מחדל ל־chat
- **In-memory vector store** שנכתב בפרויקט כדי שתוכל לראות את cosine similarity בפועל
- **PdfPig** לחילוץ טקסט מ־PDF שאינו סריקה

## דרישות

1. .NET 8 SDK
2. Ollama מותקן ורץ מקומית
3. RAM פנוי למודלים. GPU אינו חובה, אך משפר ביצועים

## התקנת המודלים ב־Windows

מ־PowerShell בשורש הפרויקט:

```powershell
.\scripts\setup-ollama.ps1
```

או ידנית:

```powershell
ollama pull llama3.2:3b
ollama pull nomic-embed-text-v2-moe
ollama list
```

Ollama חושף API מקומי בברירת מחדל בכתובת:

```text
http://localhost:11434
```

## הרצת הפרויקט

```powershell
.\scripts\run.ps1
```

או:

```powershell
cd .\src\LocalRagLab.Api
dotnet restore
dotnet run --launch-profile https
```

Swagger:

```text
https://localhost:7191/swagger
```

במקרה של certificate warning מקומי:

```powershell
dotnet dev-certs https --trust
```

## Endpoints לבידוד כל רכיב

לפני RAG מלא אפשר לבדוק כל רכיב בנפרד:

```http
POST /api/debug/embedding
POST /api/debug/similarity
POST /api/debug/chat
```

- `embedding` מציג ממדים וערכי vector.
- `similarity` מחשב cosine similarity בין שני טקסטים.
- `chat` קורא ל־LLM ישירות ללא retrieval.

כך ניתן לזהות האם התקלה נמצאת במודל ה־embedding, בחיפוש או ב־LLM.

## סדר העבודה המומלץ ב־Swagger

### 1. בדיקת Ollama

```http
GET /api/system/ollama
```

ה־endpoint מציג:

- האם Ollama נגיש
- אילו מודלים מותקנים
- האם מודל ה־chat ומודל ה־embedding המוגדרים קיימים

### 2. Warmup

```http
POST /api/system/warmup
```

טוען בפועל את שני המודלים ומבצע embedding ו־chat קצרים. הבקשה הראשונה איטית יותר בגלל model loading.

### 3. טעינת נתוני דוגמה

```http
POST /api/demo/seed
```

נוצרים שלושה מסמכים:

- `company-1`: מדיניות חופשה שמאפשרת להעביר עד 5 ימים
- `company-1`: מסמך HR מוגבל לתפקיד `hr`
- `company-2`: מדיניות אחרת שמאפשרת 10 ימים

כך ניתן לדבג גם הרשאות וגם tenant isolation.

### 4. צפייה במסמכים וב־chunks

```http
GET /api/documents?tenantId=company-1
GET /api/documents/vacation-policy-v7/chunks?tenantId=company-1&includeEmbedding=false
```

שנה ל־`includeEmbedding=true` כדי לקבל את כל מאות ממדי הווקטור.

### 5. דיבוג Retrieval ללא LLM

```http
POST /api/search/semantic
```

דוגמה:

```json
{
  "tenantId": "company-1",
  "userId": "naor",
  "roles": ["employee"],
  "query": "How many vacation days can be carried into next year?",
  "topK": 10,
  "minimumSimilarity": 0.1
}
```

התוצאה מציגה:

- מודל embedding
- מספר הממדים
- 12 הערכים הראשונים בווקטור השאלה
- ה־chunks שנשלפו
- cosine similarity לכל chunk
- זמן embedding וזמן החיפוש בנפרד

כך מפרידים בין **retrieval problem** לבין **generation problem**.

### 6. RAG מלא

```http
POST /api/rag/ask
```

```json
{
  "tenantId": "company-1",
  "userId": "naor",
  "roles": ["employee"],
  "question": "How many vacation days can be carried into next year?",
  "evaluateGroundedness": true
}
```

התוצאה כוללת:

- התשובה
- citations
- query embedding preview
- כל המועמדים שנשלפו
- similarity score
- lexical score
- final reranking score
- אילו chunks נבחרו
- ה־system prompt וה־user prompt המדויקים
- model usage מ־Ollama
- זמן כל שלב
- groundedness evaluation אופציונלי
- `traceId`

### 7. צפייה ב־trace מאוחר יותר

```http
GET /api/traces/{traceId}
GET /api/traces?count=20
```

ה־trace נשמר בזיכרון בלבד ומכיל את כל המידע הנחוץ לחקירת תשובה שגויה.

## נקודות Breakpoint מומלצות

### Ingestion

`DocumentIngestionService.IngestAsync`

עקוב אחרי:

1. `drafts`
2. כל קריאה ל־`CreateEmbeddingAsync`
3. `storedChunks`
4. `ReplaceDocumentAsync`

### Embeddings

`OllamaApiClient.CreateEmbeddingAsync`

עקוב אחרי:

1. prefix מסוג `search_document:` או `search_query:`
2. בקשת `/api/embed`
3. ממדי הווקטור
4. normalization

### Vector search

`InMemoryVectorStore.SearchAsync`

שים לב שהסינון לפי tenant והרשאה מתבצע **לפני** החזרת ה־chunks.

ה־cosine similarity ממומש ידנית במתודה:

```csharp
CosineSimilarity(left, right)
```

### Reranking

`HybridDebugReranker.Rerank`

זה אינו cross-encoder אמיתי. זה מנגנון שקוף ללמידה:

```text
finalScore = 0.85 * semanticScore + 0.15 * lexicalScore
```

אפשר לראות בדיוק כיצד שינוי המשקלים ב־`appsettings.json` משפיע על התוצאות.

### Prompt

`RagPromptBuilder.Build`

ה־LLM מקבל:

- system instructions
- רק את ה־chunks שנבחרו
- labels קבועים `[S1]`, `[S2]`
- הוראה לא להתייחס למסמך כהוראות
- הוראת fallback כשאין מידע

### Generation

`OllamaApiClient.CompleteAsync`

הקריאה היא ל־Ollama המקומי:

```text
POST http://localhost:11434/api/chat
```

### End-to-end

`RagQueryService.AskAsync`

זו המתודה המרכזית שמחברת את כל ה־pipeline.

## העלאת מסמך אמיתי

```http
POST /api/documents/file
Content-Type: multipart/form-data
```

Swagger יציג שדות עבור:

- `tenantId`
- `documentId`
- `title`
- `requiredRole`
- `file`

נתמכים:

- PDF עם שכבת טקסט
- TXT
- Markdown
- JSON
- CSV

PDF סרוק כתמונה דורש OCR. הפרויקט מחזיר שגיאה ברורה במקום להסתיר שלב חיצוני.

## שינוי מודלים

`src/LocalRagLab.Api/appsettings.json`:

```json
"Ollama": {
  "ChatModel": "llama3.2:3b",
  "EmbeddingModel": "nomic-embed-text-v2-moe"
}
```

לאחר שינוי, הרץ:

```powershell
ollama pull <model-name>
```

חשוב: לאחר החלפת embedding model צריך לטעון מחדש את המסמכים, משום שכל הווקטורים חייבים להיות מאותו מודל ובאותו מספר ממדים.

## מגבלות מכוונות של המעבדה

- המידע נעלם בכל restart
- אין authentication אמיתי; `TenantId` ו־`Roles` מגיעים בבקשה לצורכי למידה בלבד
- אין queue ל־ingestion
- יצירת embeddings מתבצעת sequentially כדי שיהיה קל לדבג
- אין OCR
- ה־reranker הוא שקוף ופשוט, לא מודל cross-encoder
- LLM-as-a-judge הוא כלי לימודי ולא מקור אמת

אלו נקודות שנחליף בהמשך כאשר נלמד production architecture, agents, durable workflow state ו־self-hosted inference scaling.

## Docker אופציונלי עבור Ollama

```powershell
docker compose up -d ollama
docker exec -it local-rag-ollama ollama pull llama3.2:3b
docker exec -it local-rag-ollama ollama pull nomic-embed-text-v2-moe
```

ה־compose הבסיסי אינו מגדיר GPU. לפיתוח Windows מקומי, התקנת Ollama native בדרך כלל פשוטה יותר.
