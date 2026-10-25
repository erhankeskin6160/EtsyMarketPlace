import { buildTaxonomyShortlist, singularizeToken, tokenizeTaxonomyText, taxonomyLeafName } from './taxonomy-shortlist';

describe('taxonomy-shortlist', () => {
  const nodes = [
    { id: 12351, name: 'Lamps', path: 'Home & Living > Lighting > Lamps' },
    { id: 12352, name: 'Night Lights', path: 'Home & Living > Lighting > Night Lights' },
    { id: 501, name: 'Headphone Stands', path: 'Electronics & Accessories > Audio > Headphone Stands' },
    { id: 502, name: 'Headphones', path: 'Electronics & Accessories > Audio > Headphones' },
    { id: 132, name: 'Shoulder Bags', path: 'Bags & Purses > Handbags > Shoulder Bags' },
    { id: 943, name: 'Mugs', path: 'Home & Living > Kitchen & Dining > Drinkware > Mugs' }
  ];

  it('singularizes common English plurals', () => {
    expect(singularizeToken('lamps')).toBe('lamp');
    expect(singularizeToken('stands')).toBe('stand');
    expect(singularizeToken('accessories')).toBe('accessory');
    expect(singularizeToken('boxes')).toBe('box');
    expect(singularizeToken('glass')).toBe('glass');
  });

  it('tokenizes and drops stopwords', () => {
    expect(tokenizeTaxonomyText('LED Headphone Stand with Color-Changing Lights'))
      .toEqual(['led', 'headphone', 'stand', 'light']);
  });

  it('ranks headphone stand above lamps for a headphone stand title', () => {
    const list = buildTaxonomyShortlist(nodes, 'LED Headphone Stand with Color-Changing Lights - Desktop Gaming Headset Holder', '');
    expect(list[0].id).toBe(501);
    expect(list.some(n => n.id === 12351)).toBeFalse();
  });

  it('uses Turkish synonyms', () => {
    const list = buildTaxonomyShortlist(nodes, 'El yapımı deri omuz çantası', '', { 'çanta': ['shoulder bags', 'handbags'] });
    expect(list[0].id).toBe(132);
  });

  it('returns empty list when there is no text', () => {
    expect(buildTaxonomyShortlist(nodes, '', '')).toEqual([]);
  });

  it('respects limit', () => {
    expect(buildTaxonomyShortlist(nodes, 'lamp light headphone mug bag', '', {}, 2).length).toBe(2);
  });

  it('extracts leaf name', () => {
    expect(taxonomyLeafName('Home & Living > Lighting > Lamps')).toBe('lamps');
  });
});
