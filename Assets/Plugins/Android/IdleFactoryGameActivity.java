package com.idlefactory.player;

import android.content.pm.ActivityInfo;

import com.unity3d.player.UnityPlayerGameActivity;

/**
 * Android entry activity that leaves runtime rotation to the operating system (T058).
 *
 * <p>With the Input System as the only active input backend, the Unity player's own auto-rotation never receives
 * accelerometer data. It requests the orientation it sees at launch as a concrete value and then never changes it,
 * so the player stops following device rotation and the rotation lock. This activity replaces every orientation
 * request with {@code fullUser}, the manifest orientation Unity derives from Auto Rotation with all four orientations
 * allowed. Android then rotates the window from its own sensor pipeline, respects the user's rotation lock and applies
 * platform rules (for example large-screen orientation overrides). Unity still receives the resulting configuration
 * change and resizes its surface, so safe area and layout follow the new orientation.
 *
 * <p>Consequence: a script-set concrete {@code Screen.orientation} has no effect on Android. The project keeps
 * Auto Rotation with all four orientations allowed (guarded by an Edit Mode test).
 */
public class IdleFactoryGameActivity extends UnityPlayerGameActivity
{
    @Override
    public void setRequestedOrientation(int requestedOrientation)
    {
        super.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_FULL_USER);
    }
}
