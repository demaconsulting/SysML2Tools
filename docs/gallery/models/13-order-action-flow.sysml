package OrderHandling {

    // A small action flow that shows the action-flow notation: a start node, an accept action,
    // a decision with a guard and an else branch, a fork and a join drawn as thick bars,
    // a send action, a done node and a terminate node.
    action def HandleOrder {
        first start then receive;

        action receive accept OrderPlaced;
        then decide stockCheck;
        if inStock then prepare;
        else reject;

        fork prepare;
        then pickItems;
        then printInvoice;

        action pickItems;
        then ready;

        action printInvoice;
        then ready;

        join ready;
        then ship;

        action ship send parcel to carrier;
        then done;

        action reject;
        then action cancel terminate;
    }

    view def OrderActionFlowView {}
}
