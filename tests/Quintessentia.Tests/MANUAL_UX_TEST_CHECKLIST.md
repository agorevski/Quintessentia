# Quintessentia Manual UX Test Checklist

This document provides a comprehensive checklist for manually testing the user experience (UX) of the Quintessentia application. These tests complement the automated unit tests and should be performed to ensure a high-quality user experience.

## Prerequisites

Before testing:
- [ ] Application is running locally via `dotnet run`
- [ ] Azure OpenAI credentials are configured in `appsettings.json`
- [ ] Browser DevTools are available for inspection
- [ ] Test with sample MP3 URL: `https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3`

---

## 1. Visual Design & Layout

### TC-UX-001: Responsive Design
- [ ] Open application on desktop (1920x1080)
  - Page displays correctly with no horizontal scroll
  - Main card is centered and properly sized
- [ ] Resize browser to tablet size (768px)
  - Layout adapts, remains readable
  - No content overflow
- [ ] Resize browser to mobile size (375px)
  - Form is usable, buttons are tappable
  - No horizontal scrolling required
  
**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-002: Branding Consistency
- [ ] Verify logo/title displays "Quintessentia" with droplet icon
- [ ] Check cyan/teal color scheme (#0891b2) is applied
- [ ] Verify tagline "Distilling audio down to its pure essence" is visible
- [ ] Check text shadows and visual effects are consistent

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-003: Card Shadow and Elevation
- [ ] Main form card has visible shadow
- [ ] Shadow creates clear depth/elevation effect
- [ ] Card stands out from background

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-004: Button States
- [ ] Hover over "Process & Summarize" button
  - Visual feedback (color change, shadow, etc.)
- [ ] Click and hold button
  - Active state shows visual change
- [ ] Try to click disabled button (while processing)
  - Cursor shows "not-allowed" state
  - Button appears visually disabled

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 2. Form Interaction

### TC-UX-005: Input Field Focus State
- [ ] Click into URL input field
- [ ] Verify clear focus ring/border appears
- [ ] Focus indicator meets accessibility contrast requirements
- [ ] Tab key moves focus logically

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-006: Placeholder Text Clarity
- [ ] Read placeholder text in URL field
- [ ] Verify it shows example format: `https://example.com/audio-episode.mp3`
- [ ] Placeholder disappears when typing begins

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-007: Required Field Validation
- [ ] Click "Process & Summarize" without entering URL
- [ ] Browser native validation message appears
- [ ] Message clearly indicates field is required

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-008: URL Format Hint
- [ ] Check help text below input field
- [ ] Text reads: "Paste a direct URL to an MP3 audio file"
- [ ] Text is readable (good contrast, appropriate size)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 3. Processing Feedback

### TC-UX-009: Button Loading State
- [ ] Enter valid URL and click "Process & Summarize"
- [ ] Button text changes to "Processing..."
- [ ] Spinner icon appears next to text
- [ ] Button is visually disabled

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-010: Form Disabled During Processing
- [ ] While processing, try to click in URL field
- [ ] Verify field is disabled
- [ ] Try to edit URL - should not be possible

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-011: Progress Bar Animation
- [ ] Watch progress bar fill during processing
- [ ] Verify smooth animation (no jumps)
- [ ] Check striped pattern is animated
- [ ] Bar height is appropriate (~25px)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-012: Progress Percentage Display
- [ ] Progress bar shows percentage text (e.g., "25%")
- [ ] Percentage updates as processing progresses
- [ ] Text is centered and readable
- [ ] Final percentage reaches 100%

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-013: Status List Updates
- [ ] Status messages appear in list as processing progresses
- [ ] Each message has appropriate icon
- [ ] Messages appear in sequence (downloading → transcribing → etc.)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-014: Status List Scrolling
- [ ] Verify list auto-scrolls to show latest update
- [ ] No manual scrolling required to see progress

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-015: Stage Icon Clarity
- [ ] Downloading: download icon (bi-download)
- [ ] Transcribing: microphone icon (bi-mic)
- [ ] Summarizing: lightning icon (bi-lightning)
- [ ] Generating speech: speaker icon (bi-volume-up)
- [ ] Complete: check circle icon (bi-check-circle-fill)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-016: Color Coding of Status Items
- [ ] In-progress items: yellow/warning color
- [ ] Completed items: green/success color
- [ ] Error items (if any): red/danger color

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 4. Settings Modal

### TC-UX-017: Modal Opening Animation
- [ ] Click settings gear icon (top-right of form)
- [ ] Modal slides in smoothly
- [ ] Background dims/blurs
- [ ] Modal is centered on screen

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-018: Custom Badge Visibility
- [ ] Save any custom setting
- [ ] Verify blue "Custom" badge appears on gear icon
- [ ] Badge position: top-right corner of icon
- [ ] Badge is clearly visible

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-019: Badge Position
- [ ] Badge doesn't obscure gear icon
- [ ] Badge size is appropriate (small but readable)
- [ ] Badge has good contrast with background

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-020: Settings Form Layout
- [ ] All fields are logically grouped
- [ ] Labels are clear and descriptive
- [ ] Input fields are properly sized
- [ ] Form is not cramped or cluttered

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-021: Info Alert Clarity
- [ ] Blue info alert at top of modal
- [ ] Message: "Override default settings from appsettings.json. Leave fields empty to use defaults."
- [ ] Alert has info icon
- [ ] Message is clear and helpful

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-022: Speed Ratio Slider
- [ ] Drag slider left and right
- [ ] Slider moves smoothly
- [ ] No lag or stuttering
- [ ] Thumb is easy to grab

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-023: Speed Ratio Value Display
- [ ] Move slider to different positions
- [ ] Value updates in real-time (e.g., "1.25x")
- [ ] Value formatted with 2 decimal places
- [ ] Display shows current multiplier (e.g., "1.50x")

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-024: Audio Format Dropdown
- [ ] Click on TTS format dropdown
- [ ] All options visible: MP3, Opus, AAC, FLAC, WAV, PCM
- [ ] Current selection is highlighted
- [ ] Selection updates when clicking an option

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-025: Modal Close Button
- [ ] Click X button in modal header
- [ ] Modal closes smoothly
- [ ] Background returns to normal
- [ ] Click outside modal (on backdrop)
- [ ] Modal also closes

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-026: Save Button Feedback
- [ ] Enter custom settings and click "Save Settings"
- [ ] Alert/confirmation appears: "Settings saved successfully!"
- [ ] Modal closes automatically
- [ ] Custom badge appears on gear icon

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-027: Reset Button Confirmation
- [ ] Click "Reset to Defaults" button
- [ ] Confirmation dialog appears
- [ ] Dialog asks: "Are you sure you want to reset all settings to defaults?"
- [ ] If confirmed, all fields clear
- [ ] If cancelled, nothing changes

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 5. Result Page UX

### TC-UX-028: Audio Player Controls
- [ ] Navigate to result page after processing
- [ ] Original audio player displays
- [ ] Click play button - audio plays
- [ ] Seek bar works (drag to different position)
- [ ] Volume control works
- [ ] Summary audio player also functional

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-029: Word Count Display
- [ ] Transcript word count visible (e.g., "1,234 words")
- [ ] Summary word count visible (e.g., "456 words")
- [ ] Numbers formatted with thousands separator
- [ ] Reduction percentage or comparison clear

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-030: Summary Text Readability
- [ ] Summary text block is visible
- [ ] Font size is readable (not too small)
- [ ] Line height provides good spacing
- [ ] Text is not too wide (good measure)
- [ ] Background provides good contrast

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-031: Processing Duration Display
- [ ] Processing duration shown (e.g., "2m 34s")
- [ ] Format is clear and readable
- [ ] Time is accurate to what was experienced

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-032: Cache Indicators
- [ ] If from cache, message states "retrieved from cache"
- [ ] Cache indicator clear and not confusing
- [ ] User understands what was cached vs. fresh

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 6. Error Handling UX

### TC-UX-033: Connection Error Message
- [ ] Disconnect network during processing
- [ ] Error message appears
- [ ] Message is user-friendly (not technical jargon)
- [ ] No stack traces or technical details exposed
- [ ] Clear guidance on what to do next

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-034: Invalid URL Feedback
- [ ] Enter "not-a-url" and submit
- [ ] Error message appears immediately
- [ ] Message explains the URL format issue
- [ ] Form not submitted to server

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-035: API Error Display
- [ ] Enter invalid API credentials in settings
- [ ] Try to process audio
- [ ] Error page displays
- [ ] Error message doesn't expose API keys or secrets
- [ ] Message is helpful (e.g., "API authentication failed")

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-036: Error Recovery
- [ ] After error, form is re-enabled
- [ ] Can edit URL and try again
- [ ] No page refresh required
- [ ] Previous input is preserved (optional)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 7. Performance & Loading

### TC-UX-037: Initial Page Load Speed
- [ ] Clear browser cache
- [ ] Navigate to homepage
- [ ] Page loads within 2 seconds
- [ ] Page is interactive quickly (no long wait)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-038: Large File Upload Feedback
- [ ] Process very large MP3 file (>50MB)
- [ ] Progress indicators work throughout
- [ ] No browser freeze or "page unresponsive" warning
- [ ] User can see that processing is ongoing

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-039: Audio Player Load Time
- [ ] Navigate to result page
- [ ] Audio players appear quickly
- [ ] No long wait before players are interactive

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 8. Accessibility

### TC-UX-040: Keyboard Navigation
- [ ] Use only keyboard (Tab, Enter, Space)
- [ ] Can navigate to all interactive elements
- [ ] Tab order is logical (top to bottom, left to right)
- [ ] Focus indicators are clearly visible
- [ ] Can submit form using Enter key
- [ ] Can activate all buttons with Space or Enter

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-041: Screen Reader Labels
- [ ] Use screen reader (NVDA, JAWS, or VoiceOver)
- [ ] All form labels are announced
- [ ] Button purposes are clear
- [ ] Error messages are announced
- [ ] Status updates are announced (ARIA live regions)

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-042: Color Contrast
- [ ] Use browser DevTools or online checker
- [ ] Check contrast of all text on background
- [ ] Verify WCAG AA compliance (4.5:1 for normal text)
- [ ] Button text has sufficient contrast
- [ ] Links are distinguishable

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-043: Form Error Announcements
- [ ] Trigger validation error (empty required field)
- [ ] Use screen reader
- [ ] Error is announced to user
- [ ] Error message is associated with field
- [ ] User can navigate to error

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 9. Mobile Experience

### TC-UX-044: Touch Target Size
- [ ] Open on mobile device or use DevTools mobile emulation
- [ ] Tap all buttons
- [ ] All buttons are at least 44x44px
- [ ] Buttons don't require precise tapping
- [ ] No accidental mis-taps

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-045: Modal on Small Screens
- [ ] Open settings modal on phone-sized screen
- [ ] Modal fits within viewport
- [ ] Can scroll through all settings
- [ ] Close button is easily tappable
- [ ] Form fields are not cut off

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-046: Audio Player on Mobile
- [ ] Use audio controls on mobile
- [ ] Play/pause button is large enough
- [ ] Seek bar is tappable and draggable
- [ ] Volume control works
- [ ] Player doesn't conflict with device audio controls

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## 10. Browser Compatibility

### TC-UX-047: Chrome Functionality
- [ ] Open in Google Chrome (latest version)
- [ ] All features work as expected
- [ ] No console errors
- [ ] Audio playback works
- [ ] Server-Sent Events work

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-048: Firefox Functionality
- [ ] Open in Mozilla Firefox (latest version)
- [ ] All features work as expected
- [ ] No console errors
- [ ] Audio playback works
- [ ] Server-Sent Events work

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-049: Safari Functionality
- [ ] Open in Safari (latest version, macOS or iOS)
- [ ] All features work as expected
- [ ] Audio playback works (various formats)
- [ ] localStorage persists settings
- [ ] No compatibility warnings

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

### TC-UX-050: Edge Functionality
- [ ] Open in Microsoft Edge (latest version)
- [ ] All features work as expected
- [ ] No console errors
- [ ] Audio playback works
- [ ] All Bootstrap components render correctly

**Result:** ✅ Pass / ❌ Fail  
**Notes:**

---

## Summary

**Total Tests:** 50  
**Passed:** ___  
**Failed:** ___  
**Pass Rate:** ___%

### Critical Issues Found
1. 
2. 
3. 

### Recommendations
1. 
2. 
3. 

**Tester Name:** _______________  
**Test Date:** _______________  
**Environment:** _______________  
**Browser Versions Tested:** _______________
