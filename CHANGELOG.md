# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.2] - 18/5/2020
### Added
- Diagnostic PSCA1100 - Reports if an NpgsqlCommand has not been assigned a SQL statement
- Configuration via .npgsqlanalyzers file
- Added docs for the new configuration file

### Fixed
- Throws an error when a connection with the database could not be established,
instead of failing silently

### Removed
- Providing connection string via an environment variable

## [0.0.1] - 14/4/2020
### Added
- Initial version
- Diagnostics PSCA1000, PSCA1001, PSCA1002
