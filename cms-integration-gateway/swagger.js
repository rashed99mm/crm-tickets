const swaggerJsdoc = require('swagger-jsdoc');

const options = {
    definition: {
        openapi: '3.0.0',
        info: {
            title: 'CommandCenter CMS - Integration Gateway Mock APIs',
            version: '1.0.0',
            description: 'Mock server for CommandCenter CMS integration gateway services (SMS, Email, Push Notifications). CommandCenter CMS is a social media and business platform focused on sustainability and carbon footprint tracking.'
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
            { name: 'Mock Admin', description: 'Dynamic mock data management' }
        ]
    },
    apis: ['./server.js', './models/*.js', './middlewares/**/*.js']
};

module.exports = swaggerJsdoc(options);
