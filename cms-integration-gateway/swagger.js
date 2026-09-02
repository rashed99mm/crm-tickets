const swaggerJsdoc = require('swagger-jsdoc');

const options = {
    definition: {
        openapi: '3.0.0',
        info: {
            title: 'CCE Carbon - Integration Gateway Mock APIs',
            version: '1.0.0',
            description: 'Mock server for CCE Carbon integration gateway services (SMS, Email, Push Notifications). CCE Carbon is a social media and business platform focused on sustainability and carbon footprint tracking.'
        },
        servers: [
            {
                url: 'http://localhost:3001',
                description: 'Local mock server'
            }
        ],
        tags: [
            { name: 'SMS', description: 'SMS gateway operations' },
            { name: 'Email', description: 'Email gateway operations' },
            { name: 'Auth', description: 'Active Directory authentication operations' },
            { name: 'KAPSARC', description: 'KAPSARC integration gateway operations' },
            { name: 'Mock Admin', description: 'Dynamic mock data management' }
        ]
    },
    apis: ['./server.js', './models/*.js', './middlewares/**/*.js']
};

module.exports = swaggerJsdoc(options);
