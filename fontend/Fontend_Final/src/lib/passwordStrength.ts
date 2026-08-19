import { ZxcvbnFactory } from "@zxcvbn-ts/core";
import * as zxcvbnCommonPackage from "@zxcvbn-ts/language-common";
import * as zxcvbnEnPackage from "@zxcvbn-ts/language-en";

// Real entropy/crack-time-based strength estimation, per OWASP Authentication Cheat Sheet's
// explicit recommendation (zxcvbn-ts) -- replaces a naive "has uppercase + digit + symbol"
// composition check, which OWASP/NIST SP 800-63B-4 both explicitly say does NOT correlate with
// real password strength and pushes users toward predictable patterns (e.g. "Password1!").
// No Vietnamese language pack exists upstream (@zxcvbn-ts/language-vi is not published), so the
// dictionary/suggestion text is English; the visible strength label in the UI is still Vietnamese.
const zxcvbn = new ZxcvbnFactory({
  translations: zxcvbnEnPackage.translations,
  graphs: zxcvbnCommonPackage.adjacencyGraphs,
  dictionary: {
    ...zxcvbnCommonPackage.dictionary,
    ...zxcvbnEnPackage.dictionary,
  },
});

export type PasswordScore = 0 | 1 | 2 | 3 | 4;

export interface PasswordStrengthResult {
  score: PasswordScore;
  suggestion: string | null;
}

// userInputs: other field values (name, email) so the estimator can penalise a password that's
// just a variation of them (e.g. "NguyenVanA123456") -- also an OWASP-recommended check.
export function estimatePasswordStrength(
  password: string,
  userInputs: string[] = []
): PasswordStrengthResult {
  if (password.length === 0) {
    return { score: 0, suggestion: null };
  }
  const result = zxcvbn.check(password, userInputs);
  return {
    score: result.score,
    suggestion: result.feedback.suggestions[0] ?? result.feedback.warning ?? null,
  };
}
