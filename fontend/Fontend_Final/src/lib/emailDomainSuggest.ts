// Non-blocking "Did you mean gmail.com?" domain-typo hint. Deliberately NOT the mailcheck.js
// library (last published 11 years ago, unmaintained) -- same technique (edit-distance against a
// list of common domains), hand-rolled here to avoid depending on an abandoned package. Never
// blocks submission: a domain that isn't in this list and isn't a near-miss is simply not flagged,
// since plenty of real domains (custom company mail, less common providers) aren't on it.
const COMMON_DOMAINS = [
  "gmail.com",
  "yahoo.com",
  "outlook.com",
  "hotmail.com",
  "icloud.com",
  "live.com",
  "msn.com",
];

function levenshteinDistance(a: string, b: string): number {
  const dp: number[][] = Array.from({ length: a.length + 1 }, (_, i) =>
    Array.from({ length: b.length + 1 }, (_, j) => (i === 0 ? j : j === 0 ? i : 0))
  );
  for (let i = 1; i <= a.length; i++) {
    for (let j = 1; j <= b.length; j++) {
      dp[i][j] =
        a[i - 1] === b[j - 1]
          ? dp[i - 1][j - 1]
          : 1 + Math.min(dp[i - 1][j], dp[i][j - 1], dp[i - 1][j - 1]);
    }
  }
  return dp[a.length][b.length];
}

const MAX_SUGGEST_DISTANCE = 2;

// Returns the full corrected email to suggest, or null if the domain already matches / isn't a
// close-enough typo of a common one. Requires a finished-looking domain (has a '.') so we don't
// flicker suggestions while the user is still mid-type.
export function suggestEmailDomain(email: string): string | null {
  const at = email.lastIndexOf("@");
  if (at === -1 || at === email.length - 1) return null;
  const local = email.slice(0, at);
  const domain = email.slice(at + 1).toLowerCase();
  if (!domain.includes(".") || COMMON_DOMAINS.includes(domain)) return null;

  let best: { domain: string; distance: number } | null = null;
  for (const candidate of COMMON_DOMAINS) {
    const distance = levenshteinDistance(domain, candidate);
    if (distance > 0 && distance <= MAX_SUGGEST_DISTANCE && (!best || distance < best.distance)) {
      best = { domain: candidate, distance };
    }
  }
  return best ? `${local}@${best.domain}` : null;
}
