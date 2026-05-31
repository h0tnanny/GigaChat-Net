## Summary

-

## Tracking

- [ ] Linked issue uses `GN-<number>` in the PR title, branch name, and commits
- [ ] GitHub Project status moved to `Review`

## Code review gate

- [ ] Self-review completed before requesting review
- [ ] Security-sensitive changes checked explicitly
- [ ] Implementation matches the linked task or bug report
- [ ] Bug fix includes a regression test, or the reason is documented below
- [ ] Feature/change includes tests, or the reason is documented below

## Verification

- [ ] `dotnet build GigaChat.Net.slnx --configuration Release`
- [ ] `dotnet test GigaChat.Net.slnx --configuration Release --no-build`

## Package impact

- [ ] No public API change
- [ ] Public API added or changed
- [ ] NuGet/package metadata changed

## Notes

-
