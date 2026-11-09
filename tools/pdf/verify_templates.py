"""Validate prepared resources and optional generated PP evaluation, without editing sources."""
from pathlib import Path
import hashlib
import json
import math
import sys
import numpy as np
import pypdfium2 as pdfium
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / 'backend/Resources/Documents'


def verify():
    manifest = json.loads((RESOURCES / 'manifest.json').read_text(encoding='utf-8'))
    for key, entry in manifest.items():
        path = RESOURCES / entry['file']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], key
        reader = PdfReader(path)
        assert len(reader.pages) == entry['pages'], key
        assert len(entry['pageSizes']) == len(reader.pages), key
        for page, size in zip(reader.pages, entry['pageSizes']):
            assert math.isclose(float(page.mediabox.width), size['width'], abs_tol=0.01), key
            assert math.isclose(float(page.mediabox.height), size['height'], abs_tol=0.01), key
        for field in entry['fields']:
            assert 0 <= field.get('page', 0) < len(reader.pages), key
            size = entry['pageSizes'][field.get('page', 0)]
            assert all(math.isfinite(field[n]) for n in ['x', 'y', 'width', 'height']), key
            assert field['x'] >= 0 and field['y'] >= 0 and field['width'] > 0 and field['height'] > 0, key
            assert field['x'] + field['width'] <= size['width'] and field['y'] + field['height'] <= size['height'], key
            assert 0 < field.get('minFontSize', 7) <= field.get('fontSize', 9), key
        assert not reader.get_fields(), key
        assert '/AcroForm' not in reader.trailer['/Root'], key
        assert all(not p.get('/Annots') for p in reader.pages), key
    application = (RESOURCES / manifest['ss-application']['file']).read_bytes()
    for secret in [b'Hector Gabriel', b'8662584004', b'I23050363', b'Heroes del 47']:
        assert secret not in application, 'Original field data leaked'
    for key in ['ss-application', 'ss-commitment', 'ss-report', 'ss-activity', 'ss-supervisor-evaluation', 'ss-self-evaluation', 'ss-termination']:
        sample = ROOT / 'tmp/pdf-qa' / f'{key}.pdf'
        if not sample.exists(): continue
        entry = manifest[key]
        with pdfium.PdfDocument(str(sample)) as result, pdfium.PdfDocument(str(RESOURCES / entry['file'])) as base:
            actual = np.array(result[0].render(scale=1).to_pil())
            expected = np.array(base[0].render(scale=1).to_pil())
            mask = np.zeros(actual.shape[:2], dtype=bool)
            for field in entry['fields']:
                x, y, w, h = [field[k] for k in ['x', 'y', 'width', 'height']]
                mask[max(0,int(y)-3):int(y+h)+4, max(0,int(x)-3):int(x+w)+4] = True
            assert np.array_equal(actual[~mask], expected[~mask]), f'{key}: changes outside authorized fields'
            text = PdfReader(sample).pages[0].extract_text()
            if key in ['ss-report', 'ss-supervisor-evaluation']:
                assert 'Ana Gómez Díaz' in text and 'Coordinadora' in text, key
            if key in ['ss-supervisor-evaluation', 'ss-self-evaluation', 'ss-termination']:
                assert 'María José Muñoz Peña' in text and 'Apoyo tecnológico' in text, key
            if key == 'ss-termination':
                assert '480' in text and 'noviembre' in text and 'Ana Gómez Díaz' in text, key
            result[0].render(scale=1.5).to_pil().save(sample.with_suffix('.png'))
    if len(sys.argv) > 1:
        generated = Path(sys.argv[1])
        reference = RESOURCES / manifest['pp-evaluation']['file']
        with pdfium.PdfDocument(str(generated)) as output, pdfium.PdfDocument(str(reference)) as base:
            assert len(output) == len(base) == 2
            assert output[0].get_size() == base[0].get_size()
            assert np.array_equal(np.array(output[1].render(scale=1).to_pil()), np.array(base[1].render(scale=1).to_pil())), 'Instruction page changed'
            out = ROOT / 'tmp/pdf-qa'
            out.mkdir(parents=True, exist_ok=True)
            output[0].render(scale=1.5).to_pil().save(out / 'pp-evaluation.png')
        text = PdfReader(generated).pages[0].extract_text()
        assert 'Muñoz' in text and '12/01/2026' in text, 'Missing generated data'
        # The generation map must never touch grades or signature areas.
        assert all(f['y'] + f['height'] < 240 for f in manifest['pp-evaluation']['fields'])
    print(f'Verified {len(manifest)} templates, privacy and PDF structure; optional PP sample verified.')


if __name__ == '__main__':
    verify()
