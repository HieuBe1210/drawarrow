# Hints and AdMob

- Main levels 1-4: unlimited hints without spending the reserve.
- Starting at main level 5: the initial reserve is 3 free hints. Each new hint spends one; tapping the same active hint again does not spend another.
- Each win at main level 5, 10, 15, 20, etc. adds one hint and displays +1 Hint on the win screen. Replay wins also award one hint. Win() prevents duplicate awards for the same result.
- Other modes share the free reserve and do not award main-campaign milestones.
- Exhausting the reserve requires a completed rewarded ad for each hint. Coins are not spent.
- Old saves with a five-hint initial grant are adjusted down by two once, clamped to zero; extra earned hints and prior spending are accounted for. The economy version prevents repeated adjustment. Older saves without a hint reserve receive three.
- Hint balances persist with progress; ad IDs remain configured for Google test ads until production IDs are supplied.

## Cấu hình quảng cáo

Đã nhúng Google Mobile Ads for Unity 11.7.0 và External Dependency Manager 2.1.1 vào `Packages/`, kèm license gốc.

Hiện dùng ID test Google:

- App ID: `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` (cũng chỉnh được trong menu Google Mobile Ads Settings).
- Rewarded Ad Unit ID: `Assets/Resources/AdMobHintSettings.json`.

Khi có ID thật, thay App ID của từng platform, điền rewarded ad unit ID tương ứng và đặt `useTestAds` thành `false`. Chưa cấu hình ID production trong phiên làm việc này.

Luồng dùng UMP để kiểm tra consent trước khi khởi tạo SDK. Chỉ callback reward mới cho phép cấp hint; callback đóng quảng cáo đơn thuần không cấp thưởng. Game tạm dừng khi tải/xem quảng cáo. Android/iOS native dependencies được External Dependency Manager xử lý khi Unity import/build.

Tài liệu gốc: https://developers.google.com/admob/unity/quick-start và https://developers.google.com/admob/unity/rewarded

## Kiểm tra

Đã biên dịch scripts bằng Roslyn với Unity references và các DLL AdMob thật. Kiểm tra standalone cho tutorial, quỹ 5 lượt, hết lượt, thưởng mốc, chống nhận trùng và giữ nguyên xu đã qua; kết quả ở `hint-economy.json`.

Chưa xác nhận Play Mode, build Android/iOS hoặc phát quảng cáo trên thiết bị.
