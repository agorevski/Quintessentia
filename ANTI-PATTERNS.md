# Development Anti-Patterns Identified

This document catalogs development anti-patterns found in the Quintessentia codebase, organized by severity and type.

---

## 🔴 Critical Issues

### 1. Hardcoded Secrets in Configuration Files
**Location:** `src/Quintessentia/appsettings.json` (lines 9, 21-22)

**Problem:** Azure Storage connection strings and API keys are committed to source control.

```json
"AzureStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=quintessentia;AccountKey=xxxxxxxx..."
"Key": "xxxxxx"
```

**Impact:** Security vulnerability - secrets exposed in version control history.

**Recommendation:** 
- Use Azure Key Vault, environment variables, or user secrets for development
- Add `appsettings.json` to `.gitignore` and provide `appsettings.template.json` instead
- Rotate compromised credentials immediately

---

## Summary

| Severity | Count | Key Issues |
|----------|-------|------------|
| 🔴 Critical | 1 | Hardcoded secrets |

---

## Recommended Priority

1. **Immediate:** Rotate compromised credentials and move secrets to secure storage
2. ~~**Medium Priority:** Split god controller into focused components~~ ✅ Completed
