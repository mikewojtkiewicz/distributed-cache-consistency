CREATE TABLE IF NOT EXISTS products (
    id          SERIAL PRIMARY KEY,
    name        TEXT        NOT NULL,
    stock       INTEGER     NOT NULL DEFAULT 0 CHECK (stock >= 0),
    price_cents INTEGER     NOT NULL DEFAULT 0,
    version     BIGINT      NOT NULL DEFAULT 1,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION bump_product_version() RETURNS TRIGGER AS $$
BEGIN
    NEW.version    := OLD.version + 1;
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_bump_product_version ON products;
CREATE TRIGGER trg_bump_product_version
    BEFORE UPDATE ON products
    FOR EACH ROW
    EXECUTE FUNCTION bump_product_version();

INSERT INTO products (name, stock, price_cents) VALUES
    ('Widget',  100, 1999),
    ('Gadget',   50, 4999),
    ('Gizmo',   250,  799)
ON CONFLICT DO NOTHING;
