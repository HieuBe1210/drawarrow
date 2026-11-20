# Quảng cáo AdMob cho Arrow

Firebase Remote Config dùng [mẫu cấu hình](ads-remote-config.template.json). Nếu dự án Firebase đã có tham số khác, hãy ghép các khóa `ads_*` vào template hiện tại trước khi publish. Mỗi ID có hậu tố `_admob` được ưu tiên hơn ID chung cùng loại. Dùng điều kiện theo nền tảng trong Firebase nếu Android và iOS có ID khác nhau. Các khóa `*_type` chọn mạng dùng giá trị `1`.

| Định dạng | Điều kiện hiển thị |
| --- | --- |
| App Open | `ads_aoa_show` cho lần mở mới, `ads_aoa_resume_game_show` cho lần trở lại từ nền. Số lần mở được lưu trên máy và tăng khi khởi động mới. Hai vị trí dùng chung cooldown 10 giây mặc định. |
| Interstitial | `ads_inter_shown_on_level_end` bật khi người chơi bấm tiếp sau khi thắng. Tần suất tính từ `ads_inter_start_level`. Ad break cần ID riêng và khoảng thời gian dương, dùng chung cooldown với interstitial cuối màn. Đặt `ads_inter_ad_break_time=0` để tắt ad break. |
| Banner | `ads_banner_show` bật banner dưới màn hình. `ads_banner_admob_type=1` dùng Large Banner, `2` dùng Adaptive Banner. |
| Rewarded | Nút nhận hint hoặc chơi tiếp sau khi thua. Nếu ID từ Remote Config để trống, dùng cấu hình hiện tại trong `Resources/AdMobHintSettings.json`. |
| MREC và Native | Dùng khoảng trống phía trên màn tải hoặc popup. Native dùng mẫu nhỏ có headline, icon và CTA, chỉ được thử nếu MREC tắt, tải lỗi hoặc vị trí thiếu chỗ. |
| Collapsible Banner | Dùng ID banner riêng, ở đáy màn hình. Chỉ yêu cầu ở lần đổi màn hoặc đóng popup đủ điều kiện sau khoảng thời gian cấu hình. Banner thường được khôi phục khi vị trí collapsible bị đóng hoặc tải lỗi. |

Các định dạng tự động có công tắc đều mặc định tắt. Ad break cũng chưa chạy vì ID mặc định để trống. Với các điều kiện theo level, game dùng level Classic hiện tại hoặc tiến độ Classic khi ở ngoài chế độ Classic.

Trước khi phát hành, gán App ID AdMob thật trong Google Mobile Ads Settings và publish ad unit ID cho các vị trí muốn bật trong Firebase. Thiết lập hiện tại dùng App ID mẫu và Rewarded ID thử nghiệm. MREC dùng một ad unit loại Banner của AdMob. [ID thử nghiệm của Google](https://developers.google.com/admob/unity/test-ads)

Khi cửa hàng đã xác nhận hoặc khôi phục giao dịch No Ads, gọi `GameApp.SetNoAdsEntitlement(bool)`. Game lưu quyền này trên máy và chặn các định dạng tự động. Rewarded vẫn được yêu cầu khi người chơi chủ động bấm nút.

## Kiểm tra trong Unity Play Mode

Mỗi lần bấm Play, Arrow tải và kích hoạt Firebase Remote Config trên màn Loading. Editor yêu cầu bản mới ngay khi vào Play và dùng cấu hình đã lưu hoặc giá trị mặc định nếu tải lỗi. Sau tối đa 8 giây chờ Firebase, game tiếp tục vào màn chính nếu mạng phản hồi chậm.

Console ghi `Firebase Remote Config ready` cùng trạng thái bật banner, trạng thái đã cấu hình ID và nguồn giá trị. `source=RemoteValue` nghĩa là tham số được đọc từ cấu hình Firebase đã kích hoạt.

Game View và Device Simulator đều hỗ trợ banner mô phỏng `AdMob banner · Local preview` trong Play Mode, kể cả khi Firebase chưa trả ID hoặc đang tắt banner. Tắt **Draw Arrow → Ads → Preview Banner When Disabled** để kiểm tra công tắc Firebase trong Editor. Trên Android và iOS, banner dùng công tắc `ads_banner_show` và ID từ Firebase.

Trong Play Mode, mở **Draw Arrow → Ads → Test All Formats** và chọn vị trí cần xem. Có hai nút App Open (mở game hoặc resume), hai nút Interstitial (cuối màn hoặc ad break), Rewarded, Banner, MREC, Native và Collapsible Banner. Các nút này chạy quảng cáo mô phỏng ngay, kể cả khi Firebase chưa bật vị trí đó. Với Rewarded, chọn hoàn thành hoặc đóng sớm để xem kết quả callback trong cửa sổ **Ads Test**. Với Collapsible Banner, bấm **Collapse** trong Game View hoặc Simulator để thu banner lại. Chọn **Return to Automatic Placements** để quay về cách chọn vị trí thông thường.
