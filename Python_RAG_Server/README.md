# Python RAG Server

Microservice FastAPI phụ trách parse tài liệu, chunking và sinh vector embedding local cho dự án PRN222 RBL Chatbot.

Tài liệu setup và kịch bản demo đầy đủ nằm trong:

```text
setup.md
```

## 1. Cài Đặt Python Backend

Phân hệ Python là microservice phụ trách các tác vụ nặng: parse tài liệu sang Markdown, chunking nội dung, và tạo vector embedding local.

### Tạo môi trường ảo

```bat
cd /d D:\ASM_PRN222\Python_RAG_Server
python -m venv venv
venv\Scripts\activate
```

Khi kích hoạt thành công, đầu dòng lệnh sẽ có dạng:

```text
(venv) D:\ASM_PRN222\Python_RAG_Server>
```

### Cài thư viện

```bat
pip install -r requirements.txt
```

Hoặc cài trực tiếp:

```bat
pip install fastapi uvicorn pydantic python-multipart sentence-transformers pandas openpyxl python-docx python-pptx pypdf tabulate
```

Lần đầu chạy, server sẽ tải 3 model từ HuggingFace về máy cục bộ:

- `BAAI/bge-m3`
- `intfloat/multilingual-e5-base`
- `keepitreal/vietnamese-sbert`

Dung lượng tải về có thể khoảng 3GB-4GB tùy cache của máy.

### Khởi chạy server

```bat
python -m uvicorn api_server:app --port 8000
```

Kiểm tra server:

```text
http://localhost:8000/api/health
```

Giữ nguyên cửa sổ CMD/PowerShell này trong khi demo để hội đồng thấy request từ web C# gọi sang Python theo thời gian thực.

## 2. Cài Đặt Web Application .NET

1. Mở solution `PRN222_Project/PRN222_Project.sln` bằng Visual Studio.
2. Kiểm tra `appsettings.json`, đảm bảo connection string trỏ đúng SQL Server local.
3. Mở Package Manager Console, chọn Default Project là tầng repository/data access.
4. Chạy migration:

```powershell
Update-Database
```

5. Bấm `F5` để chạy web application.

## 3. Troubleshooting

**Lỗi:** `'uvicorn' is not recognized as an internal or external command`  
**Cách xử lý:** Chạy bằng module Python:

```bat
python -m uvicorn api_server:app --port 8000
```

**Lỗi:** `Could not import module "api_server"`  
**Cách xử lý:** Đảm bảo terminal đang đứng trong thư mục chứa file:

```bat
cd /d D:\ASM_PRN222\Python_RAG_Server
```

**Lỗi:** server tải model quá lâu lần đầu  
**Cách xử lý:** Đây là hành vi bình thường. Giữ kết nối internet ổn định và chờ HuggingFace cache xong model.

## 4. Kịch Bản Demo Flows

Khi thuyết minh, chia màn hình thành hai nửa:

- Bên trái: trình duyệt web C#.
- Bên phải: terminal Python FastAPI.

### Flow 1: Tạo lập kho tri thức thông minh

**Thao tác UI:** Admin đăng nhập, vào "Quản lý Kho Tri Thức", chọn môn `PRN222`, upload PDF hoặc Excel, bấm "Lưu và Indexing tài liệu".

**Quan sát terminal Python:** Terminal hiện HTTP POST, parse nội dung sang Markdown, chia chunk theo heading, tạo embedding vector.

**Show DB:** Mở SQL Server Management Studio và chạy:

```sql
SELECT * FROM DocumentChunks;
```

**Lời thoại:**  
"Thưa thầy cô, hệ thống không chỉ lưu file nhị phân truyền thống. Tài liệu sau khi upload được đẩy qua API Python để chuẩn hóa thành Markdown máy đọc, sau đó chia chunk và sinh vector embedding. Cách lưu nội dung có cấu trúc kèm vector trong SQL Server giúp chatbot truy xuất đúng ngữ cảnh hơn so với việc cào text thô."

### Flow 2: Chat hỏi đáp thông minh

**Thao tác UI:** Sinh viên mở chatbot và hỏi: "Lớp trừu tượng khác gì Interface trong .NET?"

**Kết quả mong đợi:** Chatbot trả lời theo kiểu streaming và hiện nguồn trích dẫn từ tài liệu.

**Edge case:** Hỏi câu ngoài phạm vi, ví dụ: "Công thức nấu lẩu thái ngon?"  
Chatbot cần từ chối với thông báo câu hỏi nằm ngoài phạm vi tài liệu đào tạo.

**Lời thoại:**  
"Hệ thống dùng Smart Routing để khoanh vùng môn học và metadata phù hợp. Sau đó bộ lọc cosine similarity threshold giúp chặn các câu hỏi không khớp với kho tri thức, giảm nguy cơ AI bịa đặt thông tin."

### Flow 3: Làm việc nhóm thời gian thực

**Thao tác UI:** Mở hai trình duyệt, đăng nhập Admin A và Admin B, cùng vào trang quản lý câu hỏi kiểm thử.

**Thêm mới:** Admin A import file Excel có 50 câu hỏi. Admin B thấy dữ liệu mới xuất hiện mà không cần refresh.

**Sửa/Xóa:** Admin A sửa một câu hỏi, Admin B thấy dòng đó cập nhật. Admin B xóa một câu hỏi, Admin A thấy dòng biến mất.

**Lời thoại:**  
"SignalR Hub đồng bộ trạng thái giữa các client đang kết nối. Sau khi service ghi thay đổi xuống SQL Server, web app phát thông điệp tới client để cập nhật DOM theo thời gian thực."

### Flow 4: RBL Benchmark Dashboard

**Thao tác UI:** Admin vào trang benchmark, chọn `Model: bge-m3` và `Chunking: markdown_header`, bấm "Khởi chạy thực nghiệm Benchmark".

**Kết quả mong đợi:** Progress bar cập nhật liên tục, bảng kết quả hiện điểm theo từng câu hỏi, Dashboard vẽ biểu đồ bằng Chart.js.

**Lời thoại:**  
"Đây là phần nghiên cứu RBL của đồ án. Hệ thống chạy bộ câu hỏi kiểm thử, dùng LLM-as-a-Judge để chấm Faithfulness và Relevance, từ đó so sánh các cấu hình model và chunking ngay trên dashboard."
