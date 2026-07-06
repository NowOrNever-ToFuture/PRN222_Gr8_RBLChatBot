# Kiến trúc hệ thống — PRN222 Project

## Quy ước mũi tên (Sequence / Architecture diagram)

- **Request** = nét **liền** `-->` — đi từ bên gọi → bên được gọi.
- **Response** = nét **đứt** `-.->` — đi ngược lại, đúng cặp với request.

> Mỗi cặp gọi luôn gồm 1 liền + 1 đứt **giữa đúng 2 khối đó**, ngược chiều nhau.
> Không để mũi tên đứt (response) nhảy sang một cặp đối tượng khác.

## Sơ đồ kiến trúc

```mermaid
flowchart TB
    %% ===== Legend / Chú thích =====
    subgraph LEGEND["📖 Chú thích"]
        direction LR
        A1[Caller] -->|Request| A2[Callee]
        A2 -.->|Response| A1
    end

    %% ===== Các khối =====
    subgraph EXT["External Microservice (Python RAG Server)"]
        DP[Document Parser]
        EM[Embedding Models]
    end

    subgraph BIZ["Business Layer (PRN222.Services)"]
        IF[Interfaces]
        SV[Services]
        DTO[DTOs]
    end

    subgraph DAL["Data Access Layer (PRN222.Repositories + Models)"]
        REPO[Repositories]
        CTX[AppDbContext]
        ENT[Entity Models]
    end

    subgraph PRES["Presentation Layer (PRN222.RazorWebApp)"]
        PAGE[Razor Pages]
        CODEBEHIND[Code-behind]
        VM[ViewModels]
    end

    DB[(SQL Server Database)]
    USER[User Browser]

    %% ===== Request (liền) --> / Response (đứt) -.-> =====
    USER -->|Request| PRES
    PRES -.->|Response| USER

    PRES -->|Request| BIZ
    BIZ  -.->|Response| PRES

    BIZ -->|Request| DAL
    DAL -.->|Response| BIZ

    DAL -->|Request| DB
    DB  -.->|Response| DAL

    BIZ -->|Request| EXT
    EXT -.->|Response| BIZ
```
