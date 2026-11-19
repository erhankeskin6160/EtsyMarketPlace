import { buildEtsyDescriptionTemplate } from './etsy-description-template.util';

describe('buildEtsyDescriptionTemplate', () => {
  it('should build physical Etsy description template with title, first tag, and materials', () => {
    const result = buildEtsyDescriptionTemplate({
      title: 'Sauron Miniature Figurine',
      tags: ['sauron figure', 'lord of rings'],
      materials: 'Resin, Acrylic Paint',
      listingType: 'physical'
    });

    expect(result).toContain('✨ SAURON MINIATURE FIGURINE ✨');
    expect(result).toContain('unique sauron figure');
    expect(result).toContain('top-tier Resin, Acrylic Paint.');
    expect(result).toContain('• Primary Material: Resin, Acrylic Paint');
    expect(result).toContain('📦 PACKAGING & SHIPPING');
    expect(result).toContain('💬 CUSTOMER CARE & QUESTIONS');
  });

  it('should fall back to sensible defaults when inputs are empty or whitespace', () => {
    const result = buildEtsyDescriptionTemplate({
      title: '   ',
      tags: ['  ', ''],
      materials: ''
    });

    expect(result).toContain('✨ HANDCRAFTED ARTISAN PRODUCT ✨');
    expect(result).toContain('unique Artisan Decor');
    expect(result).toContain('High Quality Materials, Precision Craft');
    expect(result).toContain('📦 PACKAGING & SHIPPING');
  });

  it('should build digital download template when listingType is digital', () => {
    const result = buildEtsyDescriptionTemplate({
      title: 'Dark Fantasy Armor STL',
      tags: ['stl file'],
      materials: 'Resin / PLA',
      listingType: 'digital'
    });

    expect(result).toContain('✨ DARK FANTASY ARMOR STL ✨');
    expect(result).toContain('unique stl file');
    expect(result).toContain('• Recommended Material / Medium: Resin / PLA');
    expect(result).toContain('📥 INSTANT DIGITAL DOWNLOAD');
    expect(result).not.toContain('📦 PACKAGING & SHIPPING');
  });
});
