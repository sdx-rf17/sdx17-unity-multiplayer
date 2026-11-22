# Selling requirements — Sdx17 Multiplayer Architecture

> Pass **every** item below **before** submitting this package to the
> Unity Asset Store. Do not submit with a failing item and hope the
> review passes it — the review takes at least 5 business days, and
> a rejection costs that time again. Version 0.1.0 is **UNTESTED IN
> UNITY**: nothing in this checklist has been done yet.

## 1. Unity load test — clean project, zero errors/warnings

- [ ] Open a **clean Unity 2022.3 LTS** project (the version the
      package targets: `2022.3`, release `0f1`).
- [ ] Add the package (copy to `Packages/com.sdx17.multiplayer` or
      add as a local package) and let the Editor compile.
- [ ] Console shows **zero errors and zero warnings** from this
      package. Fix every warning the package produces — do not
      submit with warnings and hope reviewers ignore them.
- [ ] Confirm the core compiles with **no extra packages** installed
      (loopback transport path), and separately that it still
      compiles with `com.unity.netcode.gameobjects` installed (the
      adapter path — see item 2).
- [ ] Confirm the `#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE`
      guard symbol is defined after installing the Netcode package,
      and that the adapter compiles (see `Documentation~/GUIDE.md`
      FAQ Q1). If the symbol is not defined, the adapter silently
      compiles to nothing — find out why before submitting.

## 2. Netcode adapter check

- [ ] Install `com.unity.netcode.gameobjects` (version listed for
      Unity 2022.3 LTS; the README suggests `1.8.1`) in a test
      project.
- [ ] Confirm the project compiles with no errors and the adapter
      is active (not compiled to nothing).
- [ ] Run the Netcode demo steps in `README.md` (NetworkManager +
      adapter, standalone build as client, Editor as host) and
      confirm the client appears in the host's player list and a
      damage request from the client is validated on the host.
- [ ] Confirm the package still works after **removing** the Netcode
      package (adapter compiles to nothing, core works on loopback).

## 3. Demo scene plays

- [ ] Build the demo scene from `Samples~/Demo/README.md` (config,
      manager, demo GameObject) in Unity 2022.3 LTS.
- [ ] Press Play, press **H**: local player spawns at 100/100 health.
- [ ] Press **X**: server validates the damage, health drops to 90/100.
- [ ] Press **G**, then **I**: heal and item grant are logged as
      validated actions.
- [ ] Press **Q**: session stops and the player list empties.
- [ ] Add `MultiplayerSelfTest` to a test scene, press Play, and
      confirm the summary reads **18 passed, 0 failed**.
- [ ] Fix anything any of these steps finds. Re-run the full
      checklist after each fix — do not submit a package whose own
      self-test fails.

## 4. Docs proofread

- [ ] `README.md` — proofread. Every class/method name matches the
      code in `Runtime/`; no invented API.
- [ ] `Documentation~/GUIDE.md` — proofread. Quickstart works
      step-by-step in a clean project; API reference matches the
      real code; the 8 FAQ answers are accurate; limitations are
      honest (UNTESTED IN UNITY, loopback is offline/local only, no
      hosted server, no movement sync, remote-player spawn gap).
- [ ] `LISTING.md` — proofread. Short description is under 200
      characters; price, revenue figures (70% net: $27.30 at $39,
      $24.50 at $35, before tax/refunds), and support contact
      (Sdx17 / GitHub sdx-rf17, no fake email) are correct.
- [ ] `CHANGELOG.md` — version 0.1.0 entry is accurate and keeps
      the UNTESTED IN UNITY note.
- [ ] `ThirdPartyNotices.txt` — accurate (no third-party code
      bundled; Netcode is an optional, unbundled dependency).

## 5. Screenshots — at least 5, real Unity screenshots only

- [ ] At least **5 screenshots**, each **1920x1080**.
- [ ] Every screenshot is a **real Unity screenshot** of the demo
      scene / Inspector / Console from the clean-project test in
      items 1–3 — actual Editor or Play-mode captures, not renders,
      not mockups, not stock images.
- [ ] Suggested set: (1) demo scene in Play mode with the host
      running and the player spawned; (2) Console showing the
      validated damage action and the health change; (3) Inspector
      showing the `MultiplayerConfig` asset with the custom
      inspector's validation message; (4) Inspector showing the
      `MultiplayerManager` with its Config field assigned;
      (5) self-test summary line reading "18 passed, 0 failed".
- [ ] No screenshot shows an error or a warning in the Console.
- [ ] **No AI images presented as screenshots.** AI-generated
      concept art, if used anywhere, must be clearly labelled as
      concept art — never presented as a screenshot of the product
      running.

## 6. Video — 30–60 seconds, real Unity recording only

- [ ] One video, **30–60 seconds**, a **real screen recording of
      Unity** (Editor / Play mode), not a slideshow and not
      AI-generated video.
- [ ] It shows: creating the config and manager (or the finished
      scene), pressing Play, pressing **H**, pressing **X** with the
      validated damage visible in the Console, and pressing **Q** to
      shut down.
- [ ] No AI-generated footage presented as a recording of the
      product. If any AI-generated clip is included for any reason,
      it must be disclosed (see item 7).

## 7. Icon and AI-content disclosure

- [ ] Icon: **512x512** image for the listing (the Asset Store icon
      size). It must be original or properly licensed; do not use
      another product's art.
- [ ] **AI-content disclosure:** if any concept art used in the
      listing, screenshots, icon, or video was AI-generated, the
      listing must disclose that AI-generated content was used,
      following the Asset Store's AI-content rules at submission
      time. If no AI content is used, nothing needs disclosing —
      but do not claim "no AI used" unless it is true.
- [ ] No AI images are presented as screenshots of the product
      (see item 5). This is a rejection risk, not a grey area.

## 8. Publisher account, tax, and payment

- [ ] Unity publisher account set up (the account that will own
      the listing).
- [ ] **Two-factor authentication (2FA)** enabled on the publisher
      account.
- [ ] Tax information completed in the publisher account (Unity
      will not pay out without it).
- [ ] **PayPal** set up as the payout method (or the payout method
      Unity requires for your region — confirm at submission time).
- [ ] Confirm the publisher name shown on the listing is the one
      you want buyers to see (Sdx17).

## 9. Price and comparables — re-check before listing

- [ ] Re-check comparable multiplayer / networking assets on the
      Asset Store at listing time (price, scope, reviews, last
      update). Do not rely on the prices noted when `LISTING.md`
      was drafted — the market moves.
- [ ] Confirm the price in `LISTING.md` ($39 suggested, with a
      note to consider launching at $35) still makes sense against
      those comparables. Remember: a new publisher should price
      $15–$35 to get first reviews; $39 is above that band and is
      justified only as the largest/most complex system in the
      family — and only after every item above is passed.
- [ ] Confirm the 70% publisher share figures ($27.30 at $39,
      $24.50 at $35, before tax/refunds) and update them if Unity's
      revenue share has changed.
- [ ] Do not claim the price guarantees sales. It does not.

## 10. Submission facts

- [ ] Understand the review timeline: review takes **at least 5
      business days**. Do not promise buyers (or yourself) a
      listing date before approval.
- [ ] Understand the submission cap: **5 submissions per month in
      the first 6 months** as a new publisher. A rejected submission
      counts against the cap — another reason to pass every item
      above first.
- [ ] Category is set to **Tools / Game Toolkits** (suggested in
      `LISTING.md`) or the category that fits best at submission
      time — re-check the categories the Asset Store offers when
      you submit.
- [ ] The listing's support contact is real (Sdx17 / GitHub
      sdx-rf17). No fake email address is listed anywhere.
- [ ] The listing does not claim sales, reviews, or a live listing
      that does not exist. There are none yet.
- [ ] The listing's "untested in Unity" status for v0.1.0 is stated
      honestly, or the version submitted is one that has passed
      this checklist and the status text has been updated to match
      what was actually tested. Do not leave the "untested" text in
      place after testing, and do not remove it before testing.

## Sign-off

When every box above is ticked, update this file with the date each
section was passed and the Unity version used, then submit. If any
box cannot be ticked, the package is not ready — fix the underlying
issue, re-run the affected checks, and only then submit.
