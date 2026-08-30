import { TranslationKey } from '../i18n/translations';
import { MessageChannel } from '../tickets/ticket.api';

/**
 * Single authoritative channel-to-translation-key lookup table (FB-1 / CC-24).
 * Used by message timeline filters, message bubbles, and composer selectors.
 */
export const CHANNEL_TRANSLATION_KEYS: Record<MessageChannel, TranslationKey> = {
  System: 'messages.channel.system',
  Email: 'messages.channel.email',
  WhatsApp: 'messages.channel.whatsapp',
  SMS: 'messages.channel.sms',
  WebForm: 'messages.channel.webForm',
  LiveChat: 'messages.channel.liveChat',
};

export function getChannelTranslationKey(channel: MessageChannel): TranslationKey {
  return CHANNEL_TRANSLATION_KEYS[channel] ?? 'messages.channel.system';
}
