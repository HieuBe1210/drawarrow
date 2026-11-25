#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UIKit/UIKit.h>
#include <atomic>

static std::atomic<bool> trackingRequestPending(false);

extern "C" int ArrowRequestTrackingIfActive()
{
    if (@available(iOS 14, *)) {
        if (trackingRequestPending.load() ||
            ATTrackingManager.trackingAuthorizationStatus != ATTrackingManagerAuthorizationStatusNotDetermined)
            return 1;

        if (UIApplication.sharedApplication.applicationState != UIApplicationStateActive)
            return 0;

        trackingRequestPending.store(true);
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            trackingRequestPending.store(false);
        }];
    }
    return 1;
}

extern "C" bool ArrowTrackingRequestPending()
{
    return trackingRequestPending.load();
}
