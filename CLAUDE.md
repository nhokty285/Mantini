# HƯỚNG DẪN DỰ ÁN SHOP CHATBOT TEST (UNITY)

## 🚀 Khởi Tạo Cửa Sổ Chat (Bắt Buộc)
Ngay khi User gửi tin nhắn đầu tiên trong phiên chat mới, hành động ĐẦU TIÊN và BẮT BUỘC của bạn là:
- Gọi công cụ `memory_recall` để đọc toàn bộ ngữ cảnh cũ có liên quan tới task. 
- Sau khi có dữ liệu từ bộ nhớ, mới kết hợp với nội dung tin nhắn của User để xử lý.

## 📉 QUY TRÌNH TIẾT KIỆM TOKEN ĐỂ DUY TRÌ MỨC HIGH (3-4 TIẾNG)
Để duy trì phiên làm việc liên tục ở mức tư duy HIGH mà không bị cạn kiệt Token hệ thống, bạn PHẢI tuân thủ nghiêm ngặt các quy tắc nén dữ liệu sau:

1. **Quy tắc Nén Code (Code Truncation):** 
   - Khi trả về code chỉnh sửa, TUYỆT ĐỐI không viết lại cả file script. 
   - Chỉ xuất ra đúng Class/Hàm có sự thay đổi. Sử dụng comment `// ... existing code ...` hoặc `// ... giữ nguyên logic cũ ...` cho tất cả các phần còn lại.

2. **Chặn Đứng Câu Chữ Thừa (Anti-Verbosity):**
   - Không chào hỏi, không kết luận lịch sự ("Hy vọng đoạn code này giúp ích...").
   - Đi thẳng vào kết quả: Nguyên nhân lỗi là gì? Giải pháp code sửa đổi là gì? Không giải thích lại lý thuyết C# hay Unity căn bản trừ khi được yêu cầu.

3. **Cơ chế Chuyển Giao Trí Nhớ (Migration Checkpoint):**
   - Khi phát hiện đoạn chat đã quá dài (User phải cuộn chuột nhiều), hoặc sau khi hoàn thành xong 3-4 lỗi/tính năng lớn:
   - Hãy chủ động đưa ra cảnh báo cho User theo cú pháp:
     > ⚠️ **[TOKEN ALERT]:** Đoạn chat đã dài. Hãy yêu cầu tôi tổng hợp tiến độ vào `agentmemory` để bạn có thể mở một Chat mới, giúp duy trì mức tư duy HIGH thêm 3-4 tiếng.

## 🎯 Nguyên Tắc Vận Hành Cho AI Agent
Bạn có quyền truy cập vào 3 hệ thống MCP: AgentMemory, và Unity-MCP. Hãy tự động điều phối chúng theo quy trình sau mà không cần User nhắc nhở:

1. **Kiểm tra ngữ cảnh (AgentMemory):** Trước khi thực hiện bất kỳ yêu cầu nào, hãy gọi `agentmemory` để xem các ghi chú, quy định về kiến trúc Code hoặc bộ nhớ từ các phiên chat trước.

3. **Thao tác trên Unity (Unity-MCP):** Khi cần tạo GameObject, kiểm tra cấu trúc Hierarchy, hoặc chạy thử nghiệm Play Mode, hãy sử dụng các lệnh của `unity-mcp`.
4. **Quy tắc bảo trì bộ nhớ (Memory Maintenance):** - KHÔNG tạo memory mới chồng chéo nếu thông tin cũ bị thay đổi logic. Hãy dùng `memory_update` để cập nhật trực tiếp vào ghi chú cũ nhằm tránh gây nhiễu (noise) bối cảnh.
  - Khi lưu cấu trúc UI mới, chủ động tạo liên kết chéo (`memory_create_relation`) nối giữa GameObject UI trên Hierarchy với file C# Script đang điều khiển nó.

## 🖼️ QUY TRÌNH XỬ LÝ ẢNH + UNITY-MCP (TỐI ƯU TOKEN)
Khi User gửi ảnh chụp màn hình Unity kèm theo danh sách các "Key dữ liệu" (Name, Script, Component, Hierarchy Path), bạn PHẢI tối ưu hóa việc gọi lệnh MCP theo quy trình sau:

1. **Tuyệt Đối Không Quét Mò (Anti-Generic Dump):** 
   - KHÔNG ĐƯỢC dùng các lệnh quét toàn bộ cấu trúc (như dump toàn bộ Scene hierarchy) để tự tìm kiếm đối tượng.
   - Sử dụng NGAY các "Key dữ liệu" do User cung cấp để gọi trực tiếp lệnh MCP đến chính xác đối tượng đó (Ví dụ: Dùng tên chính xác để `find_object`, hoặc gọi thẳng script được chỉ định).

2. **Quy Trình Đối Chiếu Ảnh Siêu Tốc:**
   - Bước 1: Đọc danh sách Key từ User để định vị chính xác GameObject/Component trong Project.
   - Bước 2: Nhìn vào ảnh để phân tích lỗi/yêu cầu giao diện (ví dụ: Button bị lệch, thiếu Component, sai EventTrigger).
   - Bước 3: Đưa ra giải pháp sửa đổi ngay lập tức dựa trên sự kết hợp giữa Ảnh + Key + Code có sẵn.

3. **Format User Cung Cấp Mẫu (Để AI nhận diện):**
   - Khi xử lý ảnh, hãy ưu tiên đọc dữ liệu theo cấu trúc: 
     `[Target Object Name] | [Attached Script] | [Expected Action]`
4. **Cơ Chế Chặn & Nhắc Nhở Khi Thiếu Key (Strict Missing-Key Check):**
   - Nếu User đưa ra yêu cầu/ảnh lỗi nhưng THIẾU thông tin định vị cụ thể (không rõ GameObject, không rõ file Script nào quản lý):
   - BƯỚC ĐẦU TIÊN: Tuyệt đối KHÔNG quét diện rộng (Dump Hierarchy). Hãy gọi ngay memory_smart_search hoặc memory_recall để lục lại lịch sử các session trước xem tính năng/đối tượng này từng được định vị ở đâu.
   - BƯỚC THỨ HAI: Nếu tìm thấy trong bộ nhớ: Sử dụng ngay thông tin đó để gọi trực tiếp công cụ unity-mcp can thiệp vào đích (Ví dụ: Mở đúng file Script cũ).
   - Hãy dừng lại ngay lập tức và đưa ra một thông báo ngắn gọn yêu cầu User bổ sung theo checklist sau:
     > ⚠️ **[MISSING KEYS]:** Tôi đã tra cứu agentmemory nhưng chưa có dữ liệu lịch sử về đối tượng này. Vui lòng cung cấp: Tên GameObject trên Hierarchy hoặc Script quản lý logic này để tránh tốn token mò mẫm.
## CHẶN ĐỨNG HAI "HỐ ĐEN" ĐỐT TOKEN

1. **Tuyệt Đối Không Đọc Lại File Thừa (Anti-Redundant Read):**
   - Nếu thông tin về một file Script hoặc một đoạn code ĐÃ ĐƯỢC đọc ở các lượt chat trước trong cùng một phiên, bạn PHẢI tận dụng bộ nhớ ngắn hạn của mình để xử lý tiếp.
   - CẤM không được tự ý gọi lại lệnh đọc file (`read_file`, `view_script`) cho cùng một tệp tin trừ khi User có thay đổi lớn hoặc yêu cầu trực tiếp: "Hãy đọc lại file...".

2. **Quy Tắc Lọc Log Lỗi Unity (Console Log Filtering):**
   - Khi User dán log lỗi hoặc sử dụng `unity-mcp` để đọc Console Log, nếu phát hiện có các dòng log trùng lặp (Spam do hàm Update/FixedUpdate):
   - Bạn PHẢI bỏ qua các dòng trùng lặp, chỉ giữ lại đúng 1 dòng đại diện kèm theo Call Stack (Vết ngăn xếp cuộc gọi) của lỗi đó.
   - Nếu User dán quá 20 dòng log trùng nhau, hãy kích hoạt cơ chế nhắc nhở: 
     > ⚠️ **[LOG FLOOD ALERT]:** Vui lòng chỉ dán dòng lỗi đầu tiên và Call Stack đi kèm để tránh làm cạn kiệt token phiên chat mức HIGH.

## 🔒 BIỆN PHÁP BẢO VỆ TOKEN NÂNG CAO (CHẶN ĐỨNG HẾT LƯỢT SỚM)

1. **Tuyệt Đối Không Đọc File Meta/Hệ Thống (Strict File Exclusion):**
   - CẤM gọi lệnh đọc các file có đuôi `.meta`, `.csproj`, `.sln`, hoặc các file trong thư mục `Library/`, `Logs/`, `Packages/` trừ khi User yêu cầu đích danh để sửa lỗi cấu hình dự án. Chỉ tập trung vào file `.cs`, `.md`, hoặc `.json` dữ liệu.

2. **Cắt Đứt Vòng Lặp Sửa Sai (Break the Fix-Loop):**
   - Nếu bạn đã sửa một đoạn code đến lần thứ 3 mà vẫn sinh ra lỗi Compiler hoặc Logic, KHÔNG ĐƯỢC tiếp tục thử nghiệm mù quáng trong cùng một phiên chat.
   - Hãy dừng lại, tóm tắt ngắn gọn 3 hướng đã thử nghiệm thất bại vào `agentmemory` và chủ động bảo User: "Tôi đã thử 2 cách và chưa thành công. Hãy giúp tôi mở một Chat mới để tôi tiếp cận bài toán bằng một tư duy HIGH sạch sẽ hoàn toàn."

3. **Ưu Tiên Đọc Cấu Trúc (Scan Structure First):**
  - Khi cần tìm Class, dò hàm hoặc caller (ví dụ: SelectItemForCheckout), quy trình ưu tiên là: 
  - Ưu tiên 1: Dùng memory_smart_search để tìm xem vị trí/cấu trúc của hàm đó đã được lưu trong nhật ký dự án chưa.
  - Ưu tiên 2: Nếu bộ nhớ chưa lưu, hãy dùng unity_execute_code để chạy trực tiếp một đoạn mã C# ngắn quét text qua AssetDatabase.FindAssets và File.ReadAllText ngay trên Editor để có kết quả chính xác thời gian thực.

## 🏗️ Cấu Trúc Thư Mục Quan Trọng
- Toàn bộ Code C# của dự án nằm tại: `Assets/Scripts/`

## 🛠️ Quy Chuẩn Code & Tối Ưu Hiệu Năng (Bắt Buộc)
Tuyệt đối viết code theo đúng kiến trúc của dự án. Trước khi sinh code hoặc chỉnh sửa, bạn PHẢI đọc và tuân thủ nghiêm ngặt quy định trong 2 file sau:

1. **Đọc file `skill.md`**: Để áp dụng chính xác quy ước đặt tên (Naming Conventions) như _camelCase cho private field, bắt buộc dùng [SerializeField] private thay vì public, quy tắc viết APIClient, và đi qua Checklist trước khi tạo class mới.
2. **Đọc file `OPTIMIZE.md`**: Để tối ưu thuật toán và cấu trúc dữ liệu (khi nào dùng List, Dictionary, HashSet), kiểm soát bộ nhớ chống lag frame (GC allocation), hạn chế tối đa việc viết logic nặng hoặc dùng LINQ trong hàm Update().
