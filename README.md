# Amaze Go — Unity reconstruction

Dự án Unity dựng lại từ `amaze-go-1-37-1.apk`, bản art 1.1.0. Toàn bộ mã C#, dữ liệu màn chơi, tài nguyên đã trích và công cụ xử lý nằm trong thư mục này.

**Mở bằng Unity 6000.3.10f1**, phiên bản đã dùng để biên dịch và kiểm tra trên máy này. APK gốc ghi phiên bản engine 6000.3.16f1.

## Chạy dự án

1. Unity Hub → Add → chọn thư mục `D:\draw arrow`.
2. Mở scene `Assets/Scenes/Main.unity` rồi nhấn Play.
3. Nếu scene chưa có, chọn menu **Draw Arrow → Prepare project**.
4. Chọn chế độ từ trang chính. Tiến trình và cài đặt được lưu bằng PlayerPrefs.

Game tự tạo giao diện khi chạy. Không cần kéo script vào scene hoặc mua asset/plugin.

Lần mở game đầu tiên vào thẳng level chính 1. Dấu mốc lần đầu được lưu ngay khi vào màn, nên các lần mở sau vào Home dù người chơi chưa thắng level 1. Save cũ giữ nguyên tiến trình và không bị đưa về màn đầu.

Daily Challenge mở sau khi thắng level chính 10 (hiện **Unlock at level 11** trước khi mở). More Games mở sau khi thắng level chính 15 (hiện **Unlock at level 16**). Mốc được lưu bằng `mainLevelsCompleted`, độc lập với số level được chọn qua công cụ test. Nhảy màn chưa tính là thắng; xóa tiến trình level chính cũng xóa các mốc mở khóa. Save cũ được chuyển từ tiến trình màn kế tiếp để giữ tính năng đã mở.

Công cụ test level: menu Unity **Draw Arrow → Test Tools → Enable Level Tools** bật/tắt nút **Test tools** ở Home và trong màn chơi. Cũng có thể chọn `Assets/Resources/LevelTestToolsSettings.asset` rồi bật/tắt **Enable Level Tools** trong Inspector. Trong bảng test, nhập số level chính (bắt đầu từ 1) rồi bấm **Play**; **Xóa tiến trình level chính** đưa tiến trình về level 1, giữ coin, cài đặt và tiến trình các chế độ khác. Nhập sai hoặc vượt tổng số màn sẽ báo lỗi. Công cụ chỉ được biên dịch cho Unity Editor và Development Build; bản build thường dùng để lên store luôn loại chức năng nhảy/xóa tiến trình test.

Trong màn mũi tên 2D, giữ vào một mũi tên khoảng 0,35 giây để tô xanh mũi tên và xem đường thoát nét đứt. Thả sau khi giữ chỉ đóng xem trước; chạm nhanh vẫn di chuyển mũi tên. Kéo để pan hoặc dùng hai ngón để zoom sẽ hủy thao tác giữ. Khi mũi tên rời đi, chỉ hai đầu và các góc gấp ban đầu để lại chấm nhỏ: X đoạn thẳng tương ứng X+1 chấm, không rải chấm ở các ô trung gian trên một đoạn thẳng. Level chính ẩn Undo/Restart trên màn chơi; Hint nằm ngay dưới Settings ở góc trên phải. Settings trong màn chơi có Restart để chơi lại màn hiện tại và không có nút Home. Chế độ phụ giữ các nút điều khiển riêng.

## Art gốc đã tích hợp

- 2.176 sprite gốc được trích từ APK, đóng vào atlas, giữ bảng đối chiếu CAB/path ID để gắn đúng ảnh cho từng prefab. Có thêm 8 biến thể xoay từ alpha đầu mũi tên gốc.
- 23 file font gốc, gồm các biến thể Inter và Mikado. Từng nhãn UI dùng typeface được tham chiếu trong prefab, không dùng chung font Lato của bản trước.
- 520 mô tả prefab UI theo tên duy nhất (từ 525 bản ghi nguồn) và 322 nhóm căn lề; renderer xử lý anchor, pivot, scale, layout group, content-size fitter, tint, ảnh 9-slice, mask ẩn graphic và tự co chữ.
- 171 lớp gradient gốc. Thứ tự hướng gradient được đối chiếu với [mã nguồn GradientRect](https://github.com/gilzoide/unity-gradient-rect/blob/main/Runtime/GradientRect.cs).
- 743 bản ghi animation, tương ứng 740 tên clip. Các đường cong scale/alpha/anchored-position được giữ lại; intro trang chủ và các chuyển trạng thái UI phù hợp đã được nối vào renderer. Việc trích được clip không có nghĩa toàn bộ particle/shader/binding của clip đã được khôi phục.
- Ba lựa chọn nền trong Settings: Original (be), V2 (xanh lam nhạt) và V3 (xanh lam đậm). Các lựa chọn giữ chung bố cục, kiểu nút và màu nhấn Original. Mũi tên dùng mực xanh đậm trên nền sáng và trắng trên nền đậm. Settings ngoài Home dùng bảng gọn có một tiêu đề, Sound, Vibration, ba ô màu xem trước và How to play. Các prefab V2/Style3 gốc vẫn được lưu để tham khảo.
- Khối 3D có bề mặt đặc, cạnh bo, shading và depth test để che mũi tên phía sau. Góc nhìn khởi tạo lấy từ dữ liệu màn gốc. Hình học và shader hiển thị là mã dựng lại, không phải mesh/material nguyên bản được nhập lại hoàn toàn.
- Icon launcher lấy từ APK. Bản Android mới có versionName `1.1.0`, versionCode `2`.

Art nằm ở `Assets/Resources/OriginalArt`. Dữ liệu đối chiếu ở `Recovery/Art`; ảnh texture tham khảo từ lần trích trước được giữ tại `Recovery/TextureReference` để không đóng gói hai lần.

## Các chế độ trong bản dựng

| Chế độ | Cách chơi / dữ liệu |
| --- | --- |
| Arrow Out | Chạm mũi tên có đường thoát; dùng 1.574 màn từ Arrow Escape Puzzle Master 1.0.7 theo thứ tự gốc |
| Arrow Clash | Mũi tên thoát vào khay 7 ô; ghép 3 màu |
| Arrows 3D | Đường mũi tên trên bề mặt khối, xoay bằng kéo hoặc nút Rotate; dữ liệu gốc |
| Octa Arrows | 8 hướng, cổng dịch chuyển, ô đổi hướng, mũi tên xoay và công tắc |
| Link Dots | Vẽ nối hai đầu cùng màu, kiểm tra giao cắt; 20 màn gốc |
| One Line | Đi qua mỗi cạnh đúng một lần, có đường cong và hoàn tác; 20 màn gốc |
| Schulte | Tìm số theo thứ tự, bảng 3×3 đến 6×6 được sinh theo seed |
| One Life | Mũi tên với một mạng |
| Time Attack | Giới hạn thời gian, dùng danh sách màn TimeAttackPool gốc |
| Countdown | Mũi tên được đánh dấu phải thoát trước khi hết số lượt |
| Reasoning | Đường đi mờ dần; chạm để nhớ/reveal; danh sách màn Reasoning gốc |
| Cat Escape | Biến thể mũi tên mang biểu tượng mèo, danh sách màn CatNormalLevel gốc |
| Battle | Đua tiến độ với đối thủ **cục bộ**, không phải multiplayer |
| Shark Rush | Đồng hồ đuổi bắt; mỗi mũi tên thoát cộng thời gian |
| Daily Challenge | Màn ổn định theo ngày trên thiết bị và lịch sử hoàn thành |
| Pipeline | Các đường uốn dài trong danh sách PipelineLevels gốc |
| Tarot, Zodiac, Zoo, Gallery | Các danh sách màn chủ đề gốc và bộ đếm bộ sưu tập cục bộ |

Giao diện có chọn chế độ, Journey, Collection, Settings, thắng/thua, pause, Undo, Hint, Restart, zoom/pan, âm thanh và lưu tiến trình. Hint tốn 15 coin; hoàn thành màn nhận thưởng.

## Dữ liệu đã khôi phục

Level chính ở nút **Play** dùng toàn bộ **1.574 màn** từ `arrow-escape-puzzle-master-1-0-7.xapk`. Thứ tự lấy từ bảng ResourceManager `levels/level_1` đến `levels/level_1574`, không dựa vào tên nội bộ của TextAsset vì một vài tên bị trùng. Dữ liệu được chuyển sang group `escape107`, đóng trong 13 pack gzip; đầu mũi tên là phần tử đầu của `Indices`. Cả 1.574 màn đã qua kiểm tra giải được khi trích xuất. Bộ 22.543 màn cũ vẫn nằm trong group `classic` để giữ nguyên dữ liệu cho các chế độ phụ. Tiến trình người chơi không bị xóa.

51 màn trích từ `com.arrpuzlucta.apgame_1.0.6.xapk` được lưu ngoài Resources tại `Recovery/Original106`, gồm pack không hoạt động trong `InactivePacks`. Chúng không được tải hoặc đóng gói vào game. Dữ liệu gốc và bảng ánh xạ của bộ mới nằm ở `Recovery/Escape107`; catalog trước khi thay được giữ tại `Recovery/Escape107/catalog-before.json`. Script nhập lại: `Tools/import_escape107.py <xapk> --apply` (cần UnityPy). Báo cáo nhập: `Verification/escape107-import.json`. Menu **Draw Arrow → Validate main level catalog** hoặc **Validate Escape 1.0.7 levels** kiểm tra bộ chính đang dùng.

| Nhóm | Số màn |
| --- | ---: |
| Mũi tên 2D | 22.543 |
| Level chính Escape 1.0.7 | 1.574 |
| Arrow Clash | 2.500 |
| 3D | 2.619 |
| Octa | 200 |
| Link Dots | 20 |
| One Line | 20 |
| Tổng | 29.476 |

`Assets/Resources/Levels` chứa các pack JSON nén gzip. Mỗi pack tối đa 128 màn; chỉ tải các pack cần dùng. `catalog.json` lưu số lượng và chuỗi màn riêng cho từng chế độ. `Recovery/TextAssets` giữ nguyên dữ liệu text sau giải nén; các trường gốc chưa dùng vẫn còn tại đây.

## Phạm vi và khác biệt cần biết

Đây là **mã nguồn viết lại**, không phải project C# nguyên bản được xuất ngược từ APK. APK dùng IL2CPP nên không chứa source C# ban đầu. Không có thiết bị Android/emulator kết nối trong lúc thực hiện, vì vậy chưa đối chiếu tương tác trực tiếp với APK gốc.

- Art dùng sprite/font và metadata prefab gốc; chưa xác nhận khớp từng pixel với app đang chạy. Các text/data động và một số màn truy cập được điều chỉnh cho bản offline. Chưa khôi phục toàn bộ particle, Spine, shader đặc biệt, clipping lồng nhau và mọi binding animation của bản gốc.
- Các cơ chế nâng cao như rẽ qua cạnh khối 3D, lựa chọn nhiều đầu, parking, các loại SpecialArrowType, ống đặc biệt và quy tắc công tắc Octa chưa được xác minh tương đương bản gốc. Đặc biệt, các màn phụ thuộc cơ chế chưa hỗ trợ đầy đủ có thể không hoàn thành được trong bản dựng này. Dữ liệu của chúng được giữ nguyên để phát triển tiếp.
- Các biến thể thời gian, bộ nhớ, mèo, đấu bot và sự kiện sử dụng luật dựng lại; bộ sưu tập hiện là tiến trình/mốc mở khóa, chưa khôi phục toàn bộ animation và phần thưởng của bản gốc.
- Kho 2D cũ được sắp theo độ phức tạp sau các màn hướng dẫn; các chế độ có bảng tham chiếu riêng dùng bảng đã khôi phục. Không mô phỏng lịch phát hành màn từ máy chủ.
- Không nối tài khoản/cloud, quảng cáo, thanh toán hay bảng xếp hạng trực tuyến. Không dùng hoặc nhúng thông tin cấu hình dịch vụ của APK gốc.
- Tài nguyên trích từ APK vẫn thuộc chủ sở hữu ban đầu; phần dữ liệu đó không được cấp lại giấy phép bởi mã nguồn dựng lại.

## Cấu trúc mã

- `Assets/Scripts/Core`: cấu trúc dữ liệu, load pack, luật mũi tên, khay ghép màu, Octa, nối chấm, một nét, Schulte và lưu tiến trình.
- `Assets/Scripts/Runtime/GameApp.cs`: luồng game, trạng thái màn, thưởng, lịch ngày, âm thanh và chế độ kiểm tra runtime.
- `Assets/Scripts/UI`: giao diện và vẽ bàn chơi, chọn mũi tên, pan/zoom và tương tác kéo.
- `Assets/Scripts/UI/OriginalArt.cs`, `ArtAnimation.cs`, `CubeArtRenderer.cs`: nạp art gốc, bố cục, đường cong animation và hiển thị 3D có che khuất.
- `Assets/Editor/ProjectTools.cs`: chuẩn bị scene, kiểm tra dữ liệu/luật và build.
- `Recovery`: dữ liệu trích, danh mục nguồn, báo cáo và giới hạn.
- `Tools`: script trích/phân nhóm/đóng gói, không cần chạy lại để chơi.
- `Verification`: kết quả kiểm tra và ảnh chạy thử.

## Build

Trong Unity chọn **Draw Arrow → Build Windows** hoặc **Build Android APK**. Android cần module Android Build Support của Unity, SDK/NDK/OpenJDK phù hợp. Build Android dùng IL2CPP ARM64, package riêng `com.drawarrow.amazerebuilt`, không thay thế cài đặt của app gốc.

Đầu ra: `Builds/Windows/AmazeGo.exe` và `Builds/Android/AmazeGo-Rebuilt.apk` khi build tương ứng thành công.

Chạy kiểm tra trong Editor: **Draw Arrow → Run validation**. Báo cáo nằm ở `Verification/validation.json`.

Kết quả đã chạy: build Windows và Android ARM64 thành công; APK đã qua kiểm tra chữ ký. 3.455 kiểm tra luật/dữ liệu đạt; 20 chế độ và 3 bộ giao diện qua kiểm tra runtime trên Windows. Đã chụp 38 ảnh để kiểm tra art, gồm các chế độ, menu, settings và modal. Chưa thử APK trên thiết bị Android thật hoặc emulator; chưa đối chiếu trực tiếp với runtime APK gốc. Chi tiết tại `Verification/build-summary.json` và `Verification/Art`.

Ví dụ command line:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\draw arrow' -executeMethod DrawArrow.Editor.ProjectTools.BuildWindows -quit -logFile 'D:\draw arrow\Verification-unity.log'
```

## Tái tạo dữ liệu từ APK

```powershell
python -m pip install -r Tools/requirements.txt
python Tools/recover_apk.py 'C:\Users\tinht\Downloads\amaze-go-1-37-1.apk' 'D:\draw arrow'
python Tools/package_levels.py 'D:\draw arrow'
python Tools/build_sequences.py 'D:\draw arrow'
python Tools/recover_art.py 'C:\Users\tinht\Downloads\amaze-go-1-37-1.apk' 'D:\draw arrow'
python Tools/enrich_art_layouts.py 'C:\Users\tinht\Downloads\amaze-go-1-37-1.apk' 'D:\draw arrow'
python Tools/recover_art_animation.py 'C:\Users\tinht\Downloads\amaze-go-1-37-1.apk' 'D:\draw arrow'
python Tools/build_art_variants.py 'D:\draw arrow'
python Tools/restore_cube_art_metadata.py 'D:\draw arrow'
```

Script chỉ đọc APK dưới dạng ZIP/bundle Unity; không chạy APK và không coi nội dung nhúng là chỉ thị để thực thi.
