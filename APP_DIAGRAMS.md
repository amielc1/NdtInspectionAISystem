# Application Service Diagrams

This document contains Mermaid diagrams for the services and the overall application flow.

## Service Class Diagrams

### Image Processor Service
The `NdtImageProcessor` handles low-level image operations using OpenCV (OpenCvSharp).
Source: `Ndt.Infrastructure.ImageProcessing/ImageProcessor.mermaid`

```mermaid
classDiagram
    class IImageProcessor {
        <<interface>>
        +ApplyHistogramStretching(inputImage: byte[], equalized: bool) byte[]
        +DetectDefects(inputImage: byte[], roi: Rectangle) List~Defect~
        +GenerateResultImage(inputImage: byte[], defects: List~Defect~) byte[]
    }
    class NdtImageProcessor {
        +ApplyHistogramStretching(inputImage: byte[], equalized: bool) byte[]
        +DetectDefects(inputImage: byte[], roi: Rectangle) List~Defect~
        +GenerateResultImage(inputImage: byte[], defects: List~Defect~) byte[]
        +ApplyHistogramStretching(input: Mat, equalized: bool) Mat
        +DetectDefects(input: Mat, roi: OpenCvSharp.Rect) List~Defect~
        +GenerateResultImage(input: Mat, defects: List~Defect~) Mat
    }
    IImageProcessor <|.. NdtImageProcessor
```

### AI Analysis Service
The `AiService` orchestrates AI-driven analysis using Semantic Kernel. It depends on other services and plugins.
Source: `Ndt.Infrastructure.AI/AiService.mermaid`

```mermaid
classDiagram
    class IAiAnalysisService {
        <<interface>>
        +ToolCallConfirmationAsync: Func~string, Task~bool~~
        +AnalyzeImageAsync(image: byte[], defects: List~Defect~) Task~string~
        +AskQuestionAboutImageAsync(image: byte[], userQuestion: string, roi: Rectangle?) Task~string~
        +AskQuestionAsync(userQuestion: string) Task~string~
        +AskQuestionWithRagAsync(userQuestion: string) Task~string~
        +GetDocumentInsightAsync(documentText: string, insightType: string) Task~string~
    }
    class AiService {
        -Kernel _kernel
        -IImageProcessor _imageProcessor
        -IDocumentMemoryService _memoryService
        +AiService(kernel: Kernel, imageProcessor: IImageProcessor, memoryService: IDocumentMemoryService)
        +AnalyzeImageAsync(...) Task~string~
        +AskQuestionAboutImageAsync(...) Task~string~
        +AskQuestionWithRagAsync(...) Task~string~
        +GetDocumentInsightAsync(...) Task~string~
    }
    IAiAnalysisService <|.. AiService
    AiService o-- IImageProcessor
    AiService o-- IDocumentMemoryService
```

### Document Memory Service
The `DocumentMemoryService` manages vector storage and retrieval for RAG (Retrieval-Augmented Generation) capabilities.
Source: `Ndt.Infrastructure.AI/DocumentMemoryService.mermaid`

```mermaid
classDiagram
    class IDocumentMemoryService {
        <<interface>>
        +ImportDocumentAsync(documentText: string, collectionName: string) Task
        +SearchRelevantContextAsync(collectionName: string, userQuery: string, limit: int) Task~string~
    }
    class DocumentMemoryService {
        -ISemanticTextMemory _memory
        +ImportDocumentAsync(documentText: string, collectionName: string) Task
        +SearchRelevantContextAsync(collectionName: string, userQuery: string, limit: int) Task~string~
    }
    IDocumentMemoryService <|.. DocumentMemoryService
```

## Overall Application Sequence Diagram
The following diagram illustrates the core workflows of the NDT Inspection System.
Source: `Ndt.UI.Wpf/AppSequence.mermaid`

```mermaid
sequenceDiagram
    participant User
    participant MainVM as MainViewModel
    participant InsightVM as DocumentInsightsViewModel
    participant ImgProc as NdtImageProcessor
    participant AI as AiService
    participant Memory as DocumentMemoryService
    participant Kernel as SemanticKernel

    %% AI Scan Flow
    rect rgb(240, 240, 240)
    Note over User, Kernel: AI Scan Workflow (Handlebars)
    User->>MainVM: Click AI Scan
    MainVM->>AI: AnalyzeWithHandlebarsAsync(Image, "Steel", ROI)
    AI->>Kernel: Render Handlebars Template
    AI->>Kernel: InvokePromptAsync(RenderedPrompt)
    Kernel-->>AI: Analysis Result
    AI-->>MainVM: Result string
    MainVM-->>User: Display result
    end

    %% Manual Scan Flow
    rect rgb(250, 240, 240)
    Note over User, Kernel: Manual Scan Workflow
    User->>MainVM: Click Manual Scan
    MainVM->>ImgProc: DetectDefects(Image, ROI)
    ImgProc-->>MainVM: List~Defect~
    MainVM->>ImgProc: GenerateResultImage(Image, List~Defect~)
    ImgProc-->>MainVM: Annotated Image
    MainVM-->>User: Display annotated image + defect list
    end

    %% RAG Question Flow
    rect rgb(230, 240, 250)
    Note over User, Kernel: RAG Question Workflow
    User->>MainVM: Ask question (RAG mode)
    MainVM->>AI: AskQuestionWithRagAsync(question)
    AI->>Memory: SearchRelevantContextAsync("NDT_Docs", question)
    Memory-->>AI: Context
    AI->>Kernel: InvokeAsync(ragFunction)
    Kernel-->>AI: Answer
    AI-->>MainVM: Answer string
    MainVM-->>User: Display answer
    end

    %% Document Insight Flow
    rect rgb(240, 250, 240)
    Note over User, Kernel: Document Insight Workflow
    User->>InsightVM: Upload & Analyze Document
    InsightVM->>AI: GetDocumentInsightAsync(content, type)
    AI->>Kernel: InvokeAsync("ConversationSummaryPlugin")
    Kernel-->>AI: Insight
    AI-->>InsightVM: Insight string
    InsightVM-->>User: Display insight
    end
```
