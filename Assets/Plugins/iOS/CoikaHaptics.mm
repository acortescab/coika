#import <UIKit/UIKit.h>

// Impact generators indexed by style: 0 light, 1 medium, 2 heavy. Created once by CoikaHapticsPrepare.
static UIImpactFeedbackGenerator *s_generators[3];

extern "C"
{
    // Creates the three generators and warms up the Taptic Engine.
    void CoikaHapticsPrepare(void)
    {
        s_generators[0] = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        s_generators[1] = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        s_generators[2] = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        for (int i = 0; i < 3; i++)
        {
            [s_generators[i] prepare];
        }
    }

    // Fires one impact of the style (0-2) with the intensity (0-1), then prepares the generator for the next one.
    void CoikaHapticsImpact(int style, float intensity)
    {
        if (style < 0 || style > 2 || s_generators[style] == nil)
        {
            return;
        }

        [s_generators[style] impactOccurredWithIntensity:intensity];
        [s_generators[style] prepare];
    }
}
