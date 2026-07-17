using Microsoft.AspNetCore.SignalR;

namespace PRN222.Services
{
    /// <summary>
    /// Hub đẩy câu trả lời chat về client theo thời gian thực:
    /// - Answer được stream từng đoạn (ReceiveAnswerChunk) để hiện dần.
    /// - Gửi theo Clients.User(userId) nên MỌI tab của cùng một user đều
    ///   nhận được hội thoại (đa tab đồng bộ).
    /// Client vẫn gửi câu hỏi qua AJAX (giữ antiforgery + quota check),
    /// server xử lý xong thì phát kết quả qua hub này.
    /// </summary>
    public class ChatHub : Hub { }
}
