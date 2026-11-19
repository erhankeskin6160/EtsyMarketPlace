export interface EtsyDescriptionTemplateInput {
  title?: string | null;
  tags?: string[] | null;
  materials?: string | null;
  listingType?: 'physical' | 'digital';
}

export function buildEtsyDescriptionTemplate(input: EtsyDescriptionTemplateInput): string {
  const title = (input.title || '').trim() || 'Handcrafted Artisan Product';
  const primaryKeyword = (input.tags || []).map(t => (t || '').trim()).find(Boolean) || 'Artisan Decor';
  const materials = (input.materials || '').trim() || 'High Quality Materials, Precision Craft';
  const isDigital = input.listingType === 'digital';

  if (isDigital) {
    return `✨ ${title.toUpperCase()} ✨

Discover the perfect blend of craftsmanship, premium quality, and timeless design with this unique ${primaryKeyword}. Each file is meticulously prepared to deliver crisp detail and effortless results for your creative projects.

━━━━━━━━━━━━━━━━━━━━━━━━━
🌟 KEY HIGHLIGHTS & FEATURES
━━━━━━━━━━━━━━━━━━━━━━━━━
✔ High-Detail Design: Carefully engineered with clean geometry and sharp definition.
✔ Versatile Usage: Ideal for personal projects, studio builds, and custom gift creations.
✔ Tested & Ready: Prepped for smooth workflow with consistent, reliable output.
✔ Instant Access: Download immediately after purchase with no waiting time.

━━━━━━━━━━━━━━━━━━━━━━━━━
📏 SPECIFICATIONS & FORMAT
━━━━━━━━━━━━━━━━━━━━━━━━━
• Recommended Material / Medium: ${materials}
• Style: Modern Artisan / Digital Design
• Delivery: Instant Digital Download (No physical item shipped)

━━━━━━━━━━━━━━━━━━━━━━━━━
📥 INSTANT DIGITAL DOWNLOAD
━━━━━━━━━━━━━━━━━━━━━━━━━
This is a digital product — no physical item will be shipped. Once your payment is confirmed, your files will be immediately available in your Etsy Purchases & Reviews section.

━━━━━━━━━━━━━━━━━━━━━━━━━
💬 CUSTOMER CARE & QUESTIONS
━━━━━━━━━━━━━━━━━━━━━━━━━
Have questions about file formats, sizing, or customization requests? Feel free to reach out via Etsy Messages anytime — we are delighted to assist!`;
  }

  return `✨ ${title.toUpperCase()} ✨

Discover the perfect blend of craftsmanship, premium quality, and timeless design with this unique ${primaryKeyword}. Each piece is meticulously produced to bring elegance and character to your space.

━━━━━━━━━━━━━━━━━━━━━━━━━
🌟 KEY HIGHLIGHTS & FEATURES
━━━━━━━━━━━━━━━━━━━━━━━━━
✔ Premium Craftsmanship: Made with care using top-tier ${materials}.
✔ High Attention to Detail: Smooth finish, vivid colors, and durable structure.
✔ Perfect Gift Idea: A thoughtful and memorable surprise for birthdays, holidays, or special celebrations.
✔ Ready to Enjoy: Safely packed and prepped for effortless display or daily use.

━━━━━━━━━━━━━━━━━━━━━━━━━
📏 SPECIFICATIONS & MATERIALS
━━━━━━━━━━━━━━━━━━━━━━━━━
• Primary Material: ${materials}
• Style: Modern Artisan / Handcrafted
• Condition: Brand New, Made to Order

━━━━━━━━━━━━━━━━━━━━━━━━━
📦 PACKAGING & SHIPPING
━━━━━━━━━━━━━━━━━━━━━━━━━
We take immense pride in securely wrapping each item in multi-layered protective packaging to ensure safe transit directly to your doorstep. Fast dispatch with tracking number provided immediately upon shipment.

━━━━━━━━━━━━━━━━━━━━━━━━━
💬 CUSTOMER CARE & QUESTIONS
━━━━━━━━━━━━━━━━━━━━━━━━━
Have custom color, sizing, or personalization requests? Feel free to reach out via Etsy Messages anytime — we are delighted to assist!`;
}
