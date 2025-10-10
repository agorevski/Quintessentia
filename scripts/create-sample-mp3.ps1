# Create a 5+ second MP3 file with a beep tone for mock audio service
# This uses ffmpeg to generate a proper audio file with a beep

$outputFile = "..\wwwroot\sample-audio.mp3"

# Check if ffmpeg is available
try {
    $ffmpegVersion = & ffmpeg -version 2>&1 | Select-Object -First 1
    Write-Host "Using FFmpeg: $ffmpegVersion"
    
    # Generate a 5-second audio file with a 1000Hz beep tone
    # -f lavfi: Use libavfilter virtual input device
    # sine=frequency=1000:duration=5: Generate 1000Hz sine wave for 5 seconds
    # -ar 44100: Set sample rate to 44.1kHz
    # -ac 1: Mono audio
    # -b:a 128k: 128kbps bitrate
    & ffmpeg -f lavfi -i "sine=frequency=1000:duration=5" -ar 44100 -ac 1 -b:a 128k -y $outputFile 2>&1 | Out-Null
    
    if (Test-Path $outputFile) {
        $fileSize = (Get-Item $outputFile).Length
        Write-Host "Sample MP3 file created successfully at $outputFile ($fileSize bytes, 5 seconds with 1000Hz beep)"
    } else {
        Write-Error "Failed to create MP3 file"
    }
} catch {
    Write-Host "FFmpeg not found. Creating a longer silent MP3 file as fallback..."
    
    # Fallback: Create a longer MP3 file with more frames for ~5 seconds
    $bytes = @(
        # ID3v2 header
        0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x23,
        0x54, 0x53, 0x53, 0x45, 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00,
        0x03, 0x4C, 0x61, 0x76, 0x66, 0x35, 0x38, 0x2E, 0x37, 0x36,
        0x2E, 0x31, 0x30, 0x30, 0x00
    )
    
    # Create multiple MP3 frames for approximately 5 seconds
    # Each frame at 128kbps, 44.1kHz is ~418 bytes and represents ~0.026 seconds
    # We need about 190 frames for 5 seconds
    $frameData = @()
    for ($i = 0; $i -lt 190; $i++) {
        # MP3 frame header (MPEG-1 Layer 3, 128kbps, 44.1kHz, mono)
        $frameData += @(0xFF, 0xFB, 0x90, 0x00)
        # Add frame data (using pattern to simulate tone instead of silence)
        $pattern = @(0x55, 0xAA, 0x55, 0xAA) * 104  # 416 bytes per frame
        $frameData += $pattern
    }
    
    $allBytes = $bytes + $frameData
    [System.IO.File]::WriteAllBytes($outputFile, $allBytes)
    Write-Host "Fallback MP3 file created at $outputFile (~5 seconds)"
}
