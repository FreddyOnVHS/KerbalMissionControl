KMC 14.22.64b - Production authority packet regression isolation

Test-only update.

The production-authority unit regression no longer depends on parking-aware
Lambert search merely to manufacture a P/N/R ejection candidate. It creates
explicit coupled and Lambert ejection fixtures via reflection and verifies
authority priority directly:
  1. coupled P/N/R
  2. Lambert P/N/R
  3. legacy prograde-only fallback

No KMC production source changed.
