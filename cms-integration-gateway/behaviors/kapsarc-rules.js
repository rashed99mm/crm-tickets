module.exports = {
    check: (query) => {
        const countryCode = (query.countryCode || '').trim();
        const countryName = (query.countryName || '').trim();

        if (!countryCode) {
            return { code: 'MISSING_COUNTRY_CODE', message: 'countryCode query parameter is required' };
        }
        if (!countryName) {
            return { code: 'MISSING_COUNTRY_NAME', message: 'countryName query parameter is required' };
        }
        if (countryCode.length !== 3) {
            return { code: 'INVALID_COUNTRY_CODE', message: 'countryCode must be a 3-character ISO Alpha-3 code' };
        }

        return null; // success
    }
};
