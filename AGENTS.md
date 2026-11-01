# Development workflow

- After each completed development stage, validate the changes, commit them,
  and push to the configured GitHub repository. Do not include personal data,
  information bases, secrets, generated binaries or user wallpapers.
- Describe the behavior and validation in the commit message. Do not call an
  experimental build a stable release without evidence against RELEASE.md.
- Keep modifications to the user's working 1C sessions out of automated tests.
  Use a dedicated disposable base/process.
