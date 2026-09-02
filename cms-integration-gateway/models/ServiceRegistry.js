/**
 * Service Registry
 * Central registry for all mock gateway services.
 * Add new services here to auto-wire them.
 */

const smsModel = require('./SmsGatewayModel');
const emailModel = require('./EmailGatewayModel');
const adAuthModel = require('./AdAuthModel');
const kapsarcModel = require('./KapsarcServiceModel');
const erpModel = require('./ErpServiceModel');
const sendGridModel = require('./SendGridGatewayModel');
const metaWhatsAppModel = require('./MetaWhatsAppGatewayModel');
const twilioModel = require('./TwilioGatewayModel');

const registry = [];

function register(model) {
    registry.push(model);
}

function getAll() {
    return registry;
}

function getByName(name) {
    return registry.find(s => s.name === name);
}

// Register default services
register(smsModel);
register(emailModel);
register(adAuthModel);
register(kapsarcModel);
register(erpModel);
// FEAT-35 — provider-faithful channel mocks (CC-34/CC-35/CC-36).
register(sendGridModel);
register(metaWhatsAppModel);
register(twilioModel);

module.exports = {
    register,
    getAll,
    getByName
};
