/**
 * Simple ERP inbound mock used by the CMS integration flow.
 * The CRM calls this adapter, imports the records, and remains idempotent by externalId.
 */
module.exports = {
    name: 'erp-gateway',
    group: 'integrationgateway',
    description: 'Mock ERP ticket feed for CMS import testing',
    endpoints: [
        {
            path: '/integrationgateway/erp/tickets',
            method: 'GET',
            mockDataKey: 'erp-tickets',
            description: 'Retrieve tickets waiting to be imported from the ERP'
        }
    ]
};
