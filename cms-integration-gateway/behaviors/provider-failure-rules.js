/**
 * Deterministic failure triggers for the provider mocks (CC-37/CC-38).
 *
 * Deterministic, not random: the backend's bounded-retry policy can only be asserted end-to-end if
 * the same recipient fails the same way every run. The existing sms/email mocks randomise their
 * status, which cannot support that.
 */
const PERMANENT = new Set(['permanent-fail@mock.test', '+19995550000']);
const TRANSIENT = new Set(['transient-fail@mock.test', '+19995550001']);

const TRANSIENT_FAILURES_BEFORE_SUCCESS = 2;
const attempts = new Map();

module.exports = {
    /** @returns {{kind: 'permanent'|'transient', code: string, message: string}|null} */
    check: (recipient) => {
        const key = String(recipient || '').trim();

        if (PERMANENT.has(key)) {
            return { kind: 'permanent', code: 'INVALID_RECIPIENT', message: `Recipient ${key} is not deliverable` };
        }

        if (TRANSIENT.has(key)) {
            const soFar = attempts.get(key) || 0;
            if (soFar < TRANSIENT_FAILURES_BEFORE_SUCCESS) {
                attempts.set(key, soFar + 1);
                return { kind: 'transient', code: 'UPSTREAM_UNAVAILABLE', message: 'Temporarily unavailable' };
            }
            attempts.delete(key);
        }

        return null;
    },

    /** Test hook — clears the transient counters so a suite can re-run from a known state. */
    reset: () => attempts.clear(),
};
